using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Imagine.Students.Api.Models;

namespace Imagine.Students.Api.Services
{
	public class StudentsStore : IStudentsStore
	{
		public virtual Task<IEnumerable<Student>> GetStudents(Guid classroomId)
		{
			var seed = classroomId.GetHashCode();
			var randomizer = new Randomizer(seed);
			var faker = new Faker { Random = randomizer };
			var classSize = faker.Random.Int(15, 35);
			var gradeLevel = faker.PickRandom<GradeLevel>();
			var studentFaker = new StudentFaker(gradeLevel);
			var students = studentFaker.UseSeed(seed).Generate(classSize);

			return Task.FromResult(students.AsEnumerable());
		}
	}
}
