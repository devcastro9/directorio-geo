using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PruebaController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<ParameterDefinition>>> Get()
        {
            var result = await context.ParameterDefinitions.ToListAsync();
            return Ok(result);
        }
    }
}
