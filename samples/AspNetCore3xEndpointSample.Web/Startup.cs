// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AspNetCore3xEndpointSample.Web.Models;
using Castle.Components.DictionaryAdapter;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Batch;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNet.OData.Formatter.Deserialization;
using Microsoft.AspNet.OData.Formatter.Serialization;
using Microsoft.AspNet.OData.Interfaces;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Json;
using Microsoft.OData.UriParser;
using ODataPath = Microsoft.AspNet.OData.Routing.ODataPath;

namespace AspNetCore3xEndpointSample.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<CustomerOrderContext>(opt => opt.UseLazyLoadingProxies().UseInMemoryDatabase("CustomerOrderList"));

            var model = Application.GetAppEdmModel();
            services.AddOData()
                .AddModel(model);

            services.AddRouting();

            RegisterMissingServices(services, model);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            IEdmModel model = EdmModelBuilder.GetEdmModel();

            // Please add "UseODataBatching()" before "UseRouting()" to support OData $batch.
            app.UseODataBatching();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static void RegisterMissingServices(IServiceCollection services, IEdmModel model)
        {
            services.AddSingleton(model);
            services.AddSingleton<ODataSerializerProvider, DefaultODataSerializerProvider>();
            services.AddSingleton<ODataResourceSetSerializer>();
            services.AddSingleton<ODataResourceSerializer>();
            services.AddSingleton<ODataPrimitiveSerializer>();
            services.AddSingleton<IODataPathHandler, DefaultODataPathHandler>();
            services.AddSingleton<ODataMessageWriterSettings>();
            services.AddSingleton<ODataMediaTypeResolver>();
            services.AddSingleton<ODataMessageInfo>();
            services.AddSingleton<ODataPayloadValueConverter>();
            services.AddSingleton<ODataSimplifiedOptions>();
            services.AddSingleton<IJsonWriterFactory, DefaultJsonWriterFactory>();
            services.AddSingleton<IETagHandler, DefaultODataETagHandler>();
        }
    }

    internal class Application
    {
        internal static IEdmModel GetAppEdmModel()
        {
            return EdmModelBuilder.GetEdmModel();
        }
    }

    internal static class IODataBuilderExtensions
    {
        public static IODataBuilder AddModel(this IODataBuilder builder, IEdmModel model)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IApplicationModelProvider>(new ODataApplicationModelProvider(model)));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ODataPathMatcherPolicy>());
            return builder;
        }
    }

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
                            action.Filters.Add(new ODataEndpointMetadata(null, (_, __) => new ODataPath(new EntitySetSegment(element))));
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

                            action.Filters.Add(new ODataEndpointMetadata(mappings, (rvd, mapping) => new ODataPath(
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

                            action.Filters.Add(new ODataEndpointMetadata(mappings, (rvd, mapping) => new ODataPath(
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

                    foundAction.Filters.Add(new ODataEndpointMetadata(mappings, (rvd, mapping) => new ODataPath(
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

    public class ODataPathMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        public override int Order => 1000 - 101;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
            endpoints.All(e => e.Metadata.Any(m => m is ODataEndpointMetadata));

        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            if (candidates.Count > 1)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    ref var candidate = ref candidates[i];
                    if (!candidates.IsValidCandidate(i))
                    {
                        continue;
                    }

                    var oDataMetadata = candidate.Endpoint.Metadata.OfType<ODataEndpointMetadata>().FirstOrDefault();
                    try
                    {
                        var mappings = oDataMetadata.ParameterMappings;
                    }
                    catch
                    {
                        candidates.SetValidity(i, false);
                    }
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                ref var candidate = ref candidates[i];
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                var oDataMetadata = candidate.Endpoint.Metadata.OfType<ODataEndpointMetadata>().FirstOrDefault();
                if (oDataMetadata == null)
                {
                    continue;
                }

                var original = candidate.Endpoint.RequestDelegate;
                var name = candidate.Endpoint.DisplayName;

                var newEndpoint = new Endpoint(EndpointWithODataPath, candidate.Endpoint.Metadata, name);
                var originalValues = candidate.Values;
                var newValues = new RouteValueDictionary();
                foreach (var (key, value) in originalValues)
                {
                    if (key.EndsWith(".Name"))
                    {
                        var keyValue = originalValues[key.Replace(".Name", ".Value")];
                        var partName = originalValues[key];
                        var parameterName = oDataMetadata.ParameterMappings[oDataMetadata.ParameterMappings.Keys.Single(key => key.Name == (string)partName)];
                        newValues.Add(parameterName, keyValue);
                    }

                    newValues.Add(key, value);
                }

                candidates.ReplaceEndpoint(i, newEndpoint, newValues);

                Task EndpointWithODataPath(HttpContext httpContext)
                {
                    var odataPath = oDataMetadata.ODataPathFactory(httpContext.GetRouteData().Values, oDataMetadata.ParameterMappings);
                    var odata = httpContext.Request.ODataFeature();
                    odata.IsEndpointRouting = true;
                    odata.RequestContainer = httpContext.RequestServices;
                    odata.Path = odataPath;
                    odata.RouteName = name;
                    var prc = httpContext.RequestServices.GetRequiredService<IPerRouteContainer>();
                    if (!prc.HasODataRootContainer(name))
                    {
                        prc.AddRoute(odata.RouteName, "");
                    }

                    return original(httpContext);
                }
            }

            return Task.CompletedTask;

        }
    }

    public class EntityReferenceODataDeserializerProvider : DefaultODataDeserializerProvider
    {
        public EntityReferenceODataDeserializerProvider(IServiceProvider rootContainer)
            : base(rootContainer)
        {

        }

        public override ODataEdmTypeDeserializer GetEdmTypeDeserializer(IEdmTypeReference edmType)
        {
            return base.GetEdmTypeDeserializer(edmType);
        }

        public override ODataDeserializer GetODataDeserializer(Type type, HttpRequest request)
        {
            return base.GetODataDeserializer(type, request);
        }
    }
}
