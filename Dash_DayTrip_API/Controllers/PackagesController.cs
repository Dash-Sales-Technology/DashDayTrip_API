using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PackagesController : ControllerBase
    {
        private readonly ApiContext _context;

        public PackagesController(ApiContext context)
        {
            _context = context;
        }

        // GET: api/Packages or api/Packages?formId=xxx
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Package>>> GetPackages([FromQuery] string? formId)
        {
            var query = _context.Packages
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(formId))
            {
                query = query.Where(p => p.FormId == formId);
            }

            return await query.OrderBy(p => p.CreatedAt).ToListAsync();
        }

        // GET: api/Packages/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Package>> GetPackage(string id)
        {
            var package = await _context.Packages
                .FirstOrDefaultAsync(p => p.PackageId == id && !p.IsDeleted);

            if (package == null)
            {
                return NotFound();
            }

            return package;
        }

        // GET: api/Packages/form/{formId}
        [HttpGet("form/{formId}")]
        public async Task<ActionResult<IEnumerable<Package>>> GetPackagesByForm(string formId)
        {
            return await _context.Packages
                .Where(p => p.FormId == formId && !p.IsDeleted)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }

        // POST: api/Packages
        [HttpPost]
        public async Task<ActionResult<Package>> CreatePackage(Package package)
        {
            if (string.IsNullOrEmpty(package.PackageId))
            {
                package.PackageId = Guid.NewGuid().ToString();
            }

            package.CreatedAt = DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;
            package.IsDeleted = false;

            _context.Packages.Add(package);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPackage), new { id = package.PackageId }, package);
        }

        // POST: api/Packages/{id}/update  -> replace PUT with POST-based update
        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdatePackagePost(string id, [FromBody] Package package)
        {
            if (id != package.PackageId)
            {
                return BadRequest();
            }

            var existing = await _context.Packages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PackageId == id && !p.IsDeleted);

            if (existing == null)
            {
                return NotFound();
            }

            package.UpdatedAt = DateTime.UtcNow;
            _context.Entry(package).State = EntityState.Modified;
            _context.Entry(package).Property(p => p.CreatedAt).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PackageExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // POST: api/Packages/{id}/delete
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeletePackagePost(string id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null || package.IsDeleted)
            {
                return NotFound();
            }

            package.IsDeleted = true;
            package.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PackageExists(string id)
        {
            return _context.Packages.Any(p => p.PackageId == id && !p.IsDeleted);
        }
    }
}