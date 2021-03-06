﻿/*
 * Copyright 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Thinktecture.IdentityServer.AccessTokenValidation
{
    internal class ScopeRequirementMiddleware
    {
        private readonly Func<IDictionary<string, object>, Task> _next;
        private readonly IEnumerable<string> _scopes;

        public ScopeRequirementMiddleware(Func<IDictionary<string, object>, Task> next, params string[] scopes)
        {
            _next = next;
            _scopes = scopes;
        }

        public async Task Invoke(IDictionary<string, object> env)
        {
            var context = new OwinContext(env);

            // if no token was sent - no need to validate scopes
            var principal = context.Authentication.User;
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                await _next(env);
                return;
            }

            if (ScopesFound(context))
            {
                await _next(env);
                return;
            }

            context.Response.StatusCode = 403;
            context.Response.Headers.Add("WWW-Authenticate", new[] { "Bearer error=\"insufficient_scope\"" });

            EmitCorsResponseHeaders(env);
        }

        private void EmitCorsResponseHeaders(IDictionary<string, object> env)
        {
            var ctx = new OwinContext(env);
            string[] values;

            if (ctx.Request.Headers.TryGetValue("Origin", out values))
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", values);
            }

            if (ctx.Request.Headers.TryGetValue("Access-Control-Request-Method", out values))
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Method", values);
            }

            if (ctx.Request.Headers.TryGetValue("Access-Control-Request-Headers", out values))
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", values);
            }
        }

        private bool ScopesFound(OwinContext context)
        {
            var scopeClaims = context.Authentication.User.FindAll("scope");

            if (scopeClaims == null || !scopeClaims.Any())
            {
                return false;
            }

            foreach (var scope in scopeClaims)
            {
                if (_scopes.Contains(scope.Value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}