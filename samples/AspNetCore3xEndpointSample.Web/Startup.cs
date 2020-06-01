// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using AspNetCore3xEndpointSample.Web.Models;
using Castle.Components.DictionaryAdapter;
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
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Json;

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
        // Ignore this API, this is just made up for wiring up what we need.
        public static IODataBuilder AddModel(this IODataBuilder builder, IEdmModel model)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IApplicationModelProvider>(new ODataApplicationModelProvider(model)));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ODataPathMatcherPolicy>());
            return builder;
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
