using System;
using System.Threading.Tasks;
using Imagine.Students.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Imagine.Students.Api.Controllers
{
	[ApiController]
	[Route("api/classrooms/{classroomId}/students")]
	public class ClassroomStudentsController : ControllerBase
	{
		private readonly IStudentsStore _store;

		public ClassroomStudentsController(IStudentsStore store)
		{
			_store = store;
		}

		[HttpGet]
		public async Task<ActionResult> Get(Guid classroomId)
		{
			var students = await _store.GetStudents(classroomId);
			if (students == null)
			{
				return NotFound();
			}

			return Ok(students);
		}
	}
}
