using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagine.Students.Api.Models;

namespace Imagine.Students.Api.Services
{
	public interface IStudentsStore
	{
		Task<IEnumerable<Student>> GetStudents(Guid classroomId);
	}
}
