// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using ODataPath = Microsoft.AspNet.OData.Routing.ODataPath;

namespace AspNetCore3xEndpointSample.Web
{
    // The job of this thing is to map all OData paths into actions.
    // You can add SelectorModels to an action if you want to map more than one path and provide metadata specific to that path.
    internal class ODataApplicationModelProvider : IApplicationModelProvider
    {
        private readonly IEdmModel _model;

        public ODataApplicationModelProvider(IEdmModel model) => _model = model;

        public int Order => 1000 + 100;

        public void OnProvidersExecuted(ApplicationModelProviderContext context)
        {
        }

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            // Iterate over all the navigation roots of the model (just considering entitysets for simplicity)
            foreach (var element in _model.EntityContainer.Elements.Where(e => e.ContainerElementKind == EdmContainerElementKind.EntitySet).Cast<IEdmEntitySet>())
            {
                // Try and find a controller that matches by name/convention
                // This is the equivalent of IODataRoutingConvention
                var controller = context.Result.Controllers.FirstOrDefault(c => string.Equals(c.ControllerType.Name, $"{element.Name}Controller", StringComparison.OrdinalIgnoreCase));
                if (controller == null)
                {
                    continue;
                }

                // Look at the actions in the controller and continue the "matching" process
                foreach (var action in controller.Actions)
                {
                    if (string.Equals(action.ActionMethod.Name, "Get", StringComparison.OrdinalIgnoreCase))
                    {
                        // In this example we match an action like Customers into CustomersController.Get
                        if (action.Parameters.Count == 0)
                        {
                            // We go through the list of selectors and add an attribute route to the controller if none is present
                            foreach (var selector in action.Selectors)
                            {
                                if (selector.AttributeRouteModel == null)
                                {
                                    // Customers
                                    var template = element.Name;
                                    selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                                }
                            }

                            // We setup a resource filter that sets up the information in the request.
                            // This can be done in a more "endpoint" routing friendly way, where we just set some medatada on the endpoint.
                            // We don't have to parse the url with IODataPathHandler because routing already parsed it and we can construct an OData path.
                            action.Selectors.Single().EndpointMetadata.Add(new ODataEndpointMetadata(null, (_, __) => new ODataPath(new EntitySetSegment(element))));
                        }
                        else if (action.Parameters.Count == 1)
                        {
                            foreach (var selector in action.Selectors)
                            {
                                if (selector.AttributeRouteModel == null)
                                {
                                    // Customers({key})
                                    var template = $"{element.Name}({{{action.Parameters.Single().Name}}})";
                                    selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                                }

                            }

                            var mappings = new Dictionary<IEdmNamedElement, string>();
                            mappings[element.EntityType().Key().Single()] = action.Parameters.Single().Name;

                            action.Selectors.Single().EndpointMetadata.Add(new ODataEndpointMetadata(mappings, (rvd, mapping) => new ODataPath(
                                new EntitySetSegment(element),
                                new KeySegment(
                                    GetKeyValues(rvd, mapping, element),
                                    element.EntityType(),
                                    element))));
                        }
                        else if (action.Parameters.Count == 2)
                        {
                            var keyParts = element.EntityType().Key();
                            var keyPartsTemplate = string.Join(
                                ",",
                                Enumerable.Range(0, keyParts.Count())
                                    .Select(i => $"{{part{i}.Name}}={{part{i}.Value}}"));

                            foreach (var selector in action.Selectors)
                            {
                                if (selector.AttributeRouteModel == null)
                                {
                                    // ComplexKeyTypes(SectionNumber={sectionNumber},SectionSpot={sectionSpot})
                                    var template = $"{element.Name}({keyPartsTemplate})";
                                    selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                                }

                            }

                            var mappings = new Dictionary<IEdmNamedElement, string>();
                            mappings[keyParts.First()] = action.Parameters.First().Name;
                            mappings[keyParts.Skip(1).First()] = action.Parameters.Skip(1).First().Name;

                            action.Selectors.Single().EndpointMetadata.Add(new ODataEndpointMetadata(mappings, (rvd, mapping) => new ODataPath(
                                new EntitySetSegment(element),
                                new KeySegment(
                                    GetKeyValues(rvd, mapping, element),
                                    element.EntityType(),
                                    element))));
                        }
                    }
                }

                var functions = _model.GetAvailableOperationsBoundToCollection(element.EntityType());
                foreach (var operation in functions)
                {
                    var function = operation;

                    ActionModel foundAction = null;
                    foreach (var action in controller.Actions)
                    {
                        if (string.Equals(action.ActionMethod.Name, function.Name) &&
                            function.Parameters.Skip(1).Count() == action.Parameters.Count &&
                            function.Parameters.Skip(1).All(p => p.Type.AsPrimitive().IsInt32() && action.Parameters.First().ParameterType == typeof(int) ||
                            p.Type.AsPrimitive().IsString() && action.Parameters.First().ParameterType == typeof(string)))
                        {
                            foundAction = action;
                        }
                    }

                    if (foundAction == null)
                    {
                        continue;
                    }

                    var keyPartsTemplate = string.Join(
                        ",",
                        Enumerable.Range(0, foundAction.Parameters.Count())
                            .Select(i => $"{{fnp{i}.Name}}={{fnp{i}.Value}}"));

                    foreach (var selector in foundAction.Selectors)
                    {
                        if (selector.AttributeRouteModel == null)
                        {
                            // ComplexKeyTypes/BestComplexKeyType(value={sectionNumber},multiplier={sectionSpot})
                            var template = $"{element.Name}/{function.ShortQualifiedName()}({keyPartsTemplate})";
                            selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                        }
                    }

                    var mappings = new Dictionary<IEdmNamedElement, string>();
                    var parameters = function.Parameters.Skip(1);
                    mappings[parameters.First()] = foundAction.Parameters.First().Name;
                    if (parameters.Count() == 2)
                    {
                        mappings[parameters.Skip(1).First()] = foundAction.Parameters.Skip(1).First().Name;
                    }

                    foundAction.Selectors.Single().EndpointMetadata.Add(new ODataEndpointMetadata(mappings, (rvd, mapping) => new ODataPath(
                        new EntitySetSegment(element),
                        new OperationSegment(function, rvd
                            .Where(k => parameters.Any(p => string.Equals(p.Name, k.Key, StringComparison.OrdinalIgnoreCase)))
                            .Select(p => new OperationSegmentParameter(p.Key, new ConstantNode(GetParsedValue(p), (string)p.Value))), element))));
                }
            }

            static Dictionary<string, object> GetKeyValues(
                RouteValueDictionary rvd,
                IDictionary<IEdmNamedElement, string> mapping, IEdmEntitySet element)
            {
                var key = element.EntityType().Key();
                var result = new Dictionary<string, object>();
                foreach (var component in key)
                {
                    var routeValueName = mapping[component];
                    if (rvd.TryGetValue(routeValueName, out var value))
                    {
                        result[component.Name] = value;
                    }
                }

                return result;
            }

            static object GetParsedValue(KeyValuePair<string, object> p)
            {
                if (int.TryParse((string)p.Value, out var number))
                {
                    return number;
                }

                return p.Value.ToString();
            }
        }
    }
}
