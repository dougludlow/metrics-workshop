using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagine.Students.Api.Models;
using Prometheus;

namespace Imagine.Students.Api.Services
{
	public class TrackedStudentsStore : IStudentsStore
	{
		private const string MetricName = "student_api_operation_duration_seconds";

		private const string MetricDescrption = "student_api_operation_duration_seconds";

		private static readonly HistogramConfiguration Config = new HistogramConfiguration
		{
			StaticLabels = new()
			{
				{ "class", nameof(TrackedStudentsStore) },
				{ "method", nameof(GetStudents) }
			}
		};

		private static readonly Histogram Histogram = Metrics.CreateHistogram(MetricName, MetricDescrption, Config);

		private readonly IStudentsStore _store;

		public TrackedStudentsStore(IStudentsStore store)
		{
			_store = store;
		}

		public async Task<IEnumerable<Student>> GetStudents(Guid classroomId)
		{
			using (Histogram.NewTimer())
			{
				return await _store.GetStudents(classroomId);
			}
		}
	}
}
