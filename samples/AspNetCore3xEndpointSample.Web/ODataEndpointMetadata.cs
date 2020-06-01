// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using ODataPath = Microsoft.AspNet.OData.Routing.ODataPath;

namespace AspNetCore3xEndpointSample.Web
{
    public class ODataEndpointMetadata : IFilterMetadata
    {
        public ODataEndpointMetadata(IDictionary<IEdmNamedElement, string> parameterMappings, Func<RouteValueDictionary, IDictionary<IEdmNamedElement, string>, ODataPath> odataPathFactory)
        {
            ParameterMappings = parameterMappings;
            ODataPathFactory = odataPathFactory;
        }

        public IDictionary<IEdmNamedElement, string> ParameterMappings { get; }
        public Func<RouteValueDictionary, IDictionary<IEdmNamedElement, string>, ODataPath> ODataPathFactory { get; }
    }
}
