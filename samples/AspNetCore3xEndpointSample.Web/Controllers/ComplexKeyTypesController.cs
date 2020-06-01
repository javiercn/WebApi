using AspNetCore3xEndpointSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore3xEndpointSample.Web.Controllers
{
    public class ComplexKeyTypesController : ODataController
    {
        public IActionResult Get(int sectionNumber, int sectionSpot) =>
            Ok(new ComplexKeyType { SectionNumber = sectionNumber, SectionSpot = sectionSpot });

        public IActionResult BestComplexKeyType(int value, int multiplier)
        {
            return Ok(new ComplexKeyType { SectionNumber = value * multiplier, SectionSpot = value * multiplier });
        }

        public IActionResult BestComplexKeyType(string something)
        {
            var value = -1;
            if (something == "'lots'")
            {
                value = 9;
            }
            if (something == "'little'")
            {
                value = 3;
            }

            return Ok(new ComplexKeyType { SectionNumber = value, SectionSpot = value });
        }

        //public IActionResult BestComplexKeyType(int something)
        //{
        //    return Ok(new ComplexKeyType { SectionNumber = something, SectionSpot = something * 3 });
        //}
    }
}
