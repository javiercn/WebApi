// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.OData.Edm;

namespace AspNetCore3xEndpointSample.Web.Models
{
    public static class EdmModelBuilder
    {
        private static IEdmModel _edmModel;

        public static IEdmModel GetEdmModel()
        {
            if (_edmModel == null)
            {
                var builder = new ODataConventionModelBuilder();
                builder.EntitySet<Customer>("Customers");
                builder.EntitySet<Order>("Orders");

                builder.EntitySet<ComplexKeyType>("ComplexKeyTypes")
                    .EntityType
                    .HasKey(ckt => new { ckt.SectionNumber, ckt.SectionSpot });

                var function = builder.EntitySet<ComplexKeyType>("ComplexKeyTypes")
                    .EntityType
                    .Collection
                    .Function("BestComplexKeyType");

                function.Parameter<int>("value");
                function.Parameter<int>("multiplier");
                function.ReturnsFromEntitySet<ComplexKeyType>("ComplexKeyTypes");

                var overload = builder.EntitySet<ComplexKeyType>("ComplexKeyTypes")
                    .EntityType
                    .Collection
                    .Function("BestComplexKeyType");

                overload.Parameter<string>("something");
                overload.ReturnsFromEntitySet<ComplexKeyType>("ComplexKeyTypes");

                var differentType = builder.EntitySet<ComplexKeyType>("ComplexKeyTypes")
                    .EntityType
                    .Collection
                    .Function("BestComplexKeyType");

                differentType.Parameter<int>("something");
                differentType.ReturnsFromEntitySet<ComplexKeyType>("ComplexKeyTypes");

                _edmModel = builder.GetEdmModel();
            }

            return _edmModel;
        }

    }
}