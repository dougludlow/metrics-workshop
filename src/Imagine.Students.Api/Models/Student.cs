using System;

namespace Imagine.Students.Api.Models
{
	public class Student
	{
		public Guid Id { get; set; }

		public string FirstName { get; set; }

		public string LastName { get; set; }

		public string Username { get; set; }

		public GradeLevel Grade { get; set; }
	}
}
