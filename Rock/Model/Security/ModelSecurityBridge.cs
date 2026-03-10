// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

using Rock.Model;

namespace Rock.Security
{
    internal static class ModelSecurityBridge
    {
        private static readonly ConcurrentDictionary<Type, Func<ISecured, ISecured>> _parentAuthority =
            new ConcurrentDictionary<Type, Func<ISecured, ISecured>>();

        private static readonly ConcurrentDictionary<Type, Func<ISecured, string, Person, bool>> _isAuthorized =
            new ConcurrentDictionary<Type, Func<ISecured, string, Person, bool>>();

        public static bool TryIsAuthorized( Type modelType, ISecured secured, string action, Person person, out bool result )
        {
            var fn = _isAuthorized.GetOrAdd( modelType, CreateIsAuthorizedDelegate );
            if ( fn == null )
            {
                result = default;
                return false;
            }

            result = fn( secured, action, person );
            return true;
        }

        public static bool TryGetParentAuthority( Type modelType, ISecured secured, out ISecured parentAuthority )
        {
            var fn = _parentAuthority.GetOrAdd( modelType, CreateParentAuthorityDelegate );
            if ( fn == null )
            {
                parentAuthority = null;
                return false;
            }

            parentAuthority = fn( secured );
            return true;
        }

        private static Func<ISecured, string, Person, bool> CreateIsAuthorizedDelegate( Type modelType )
        {
            var mi = modelType.GetMethod(
                "IsAuthorizedShared",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof( ISecured ), typeof( string ), typeof( Person ) },
                modifiers: null );

            if ( mi == null || mi.ReturnType != typeof( bool ) )
            {
                return null;
            }

            var securedParam = Expression.Parameter( typeof( ISecured ), "secured" );
            var actionParam = Expression.Parameter( typeof( string ), "action" );
            var personParam = Expression.Parameter( typeof( Person ), "person" );
            var call = Expression.Call( mi, securedParam, actionParam, personParam );

            return Expression.Lambda<Func<ISecured, string, Person, bool>>( call, securedParam, actionParam, personParam )
                .Compile();
        }

        private static Func<ISecured, ISecured> CreateParentAuthorityDelegate( Type modelType )
        {
            var mi = modelType.GetMethod(
                "GetParentAuthorityShared",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof( ISecured ) },
                modifiers: null );

            if ( mi == null || mi.ReturnType != typeof( ISecured ) )
            {
                return null;
            }

            var securedParam = Expression.Parameter( typeof( ISecured ), "secured" );
            var call = Expression.Call( mi, securedParam );

            return Expression.Lambda<Func<ISecured, ISecured>>( call, securedParam ).Compile();
        }
    }
}