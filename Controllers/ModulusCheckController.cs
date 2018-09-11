using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKModulusCheckingAPI.Models;

namespace UKModulusCheckingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModulusCheckController : ControllerBase
    {
        // POST api/ModulusCheck
        [HttpPost]
        public ActionResult<ModulusChecker.ValidationResult> Post([FromBody] ModulusChecker.ValidationRequest req)
        {
            if (!ModelState.IsValid)
            {
                return new BadRequestObjectResult(ModelState);
            }
            try
            {
                return ModulusChecker.Validate(req);
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(new { error = e.Message });
            }
        }
    }
}
