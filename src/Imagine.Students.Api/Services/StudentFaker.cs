using Bogus;
using Imagine.Students.Api.Models;

namespace Imagine.Students.Api.Services
{
	public class StudentFaker : Faker<Student>
	{
		public StudentFaker(GradeLevel? gradeLevel = null)
		{
			RuleFor(s => s.Id, (f, s) => f.Random.Guid());
			RuleFor(s => s.FirstName, (f, s) => f.Name.FirstName());
			RuleFor(s => s.LastName, (f, s) => f.Name.LastName());
			RuleFor(s => s.Username, (f, s) => f.Internet.UserName(s.FirstName, s.LastName));

			if (gradeLevel == null)
			{
				RuleFor(s => s.Grade, f => f.PickRandom<GradeLevel>());
			}
			else
			{
				RuleFor(s => s.Grade, gradeLevel);
			}
		}
	}
}
