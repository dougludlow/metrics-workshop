using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagine.Students.Api.Models;

namespace Imagine.Students.Api.Services
{
	public class BrokenStudentsStore : IStudentsStore
	{
		private readonly IStudentsStore _store;

		public BrokenStudentsStore(IStudentsStore store)
		{
			_store = store;
		}

		public async Task<IEnumerable<Student>> GetStudents(Guid classroomId)
		{
			var random = new Random();
			var result = random.Next(4);

			await Task.Delay((random.Next(4) + 1) * 500); // .5s - 6s 

			switch (result)
			{
				case 0:
					return null; // 404

				case 1:
					throw new Exception("Error retrieving students from store."); // 500

				default:
					return await _store.GetStudents(classroomId); // 200
			}
		}
	}
}
