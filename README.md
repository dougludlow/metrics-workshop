# Application Metrics with Prometheus - Workshop

A sample project for demonstrating how to instrument and graph metrics for a
.NET 5 web API using [Prometheus](https://prometheus.io) and
[Grafana](https://grafana.com/).

Read the [Overview](OVERVIEW.md) for additional context.

## Prerequisites

- [Install Docker](https://docs.docker.com/get-docker/)

  - **NOTE:** If you are using Windows, we recommend following the instructions
    to use
    [Docker Desktop with WSL](https://docs.docker.com/docker-for-windows/wsl/).

- Clone this repository.

- Pull the required Docker images.

  ```bash
  docker-compose pull
  ```

## tl;dr

Don't have much time? Feel free to play around with the
[finished version](https://github.com/dougludlow/metrics-workshop/tree/final) of
this tutorial.

## Workshop

Follow the steps below to become more familiar with Prometheus. We'll be
instrumenting a .NET 5 API, creating a Grafana dashboard, and configuring
alerts. We'll also walk through how to track custom metrics. The demo API
returns a list of students for a given classroom.

### Starting Up the Services

1. Start by bringing up the necessary containers in the background.

   ```bash
   docker-compose up -d api grafana
   ```

1. Check the status of the containers and ensure they are all running. You
   should see [`prometheus`](http://localhost:9090),
   [`grafana`](http://localhost:3000), [`cadvisor`](http://localhost:8080) and
   [`api`](http://localhost:5000):

   ```bash
   docker-compose ps
   ```

1. Navigate to the `api` service's [Swagger](http://localhost:5000/swagger) to
   ensure it's working properly:

   Feel free to play around and familiarize yourself with the API. Here are some
   classroom IDs you can test with:

   ```
   2ae08889-59d0-4d2a-920a-083ca2dba1a7
   c083af68-069c-4df6-83f5-5fc138ab7283
   8e1b7e7e-6d93-408f-9a4e-6fe68a4efa47
   ```

   In fact, you can just navigate to this url to make a request:

   http://localhost:5000/api/classrooms/2ae08889-59d0-4d2a-920a-083ca2dba1a7/students

   You'll probably notice that the API is a bit buggy; requests take a long time
   and there are intermittent errors. That is part of the demo, we'll be fixing
   it later.

### Installing `prometheus-net` ([see changes](https://github.com/dougludlow/metrics-workshop/commit/c07702aeb56bb9d962e8b0db53790b8ed98e1a12))

The `prometheus-net.AspNetCore` NuGet package is used to expose metrics to
Prometheus via a `/metrics` endpoint. Let's go ahead and install it and
configure it to work with our API.

**NOTE:** An alternative library that is commonly used to expose metrics for
.NET is [AppMetrics](https://www.app-metrics.io/).

1.  First, let's attach to the `api` service's logs so we can observe build
    changes.

    ```bash
    docker-compose logs -f api
    ```

1.  In another terminal, install the `prometheus-net.AspNetCore` NuGet package
    using `docker-compose run` and the `dotnet` CLI:

    ```bash
    docker-compose run --rm api dotnet add ./src/Imagine.Students.Api package prometheus-net.AspNetCore
    ```

1.  Configure the application to use `prometheus-net` by modifying the
    [`src/Imagine.Students.Api/Startup.cs`](src/Imagine.Students.Api/Startup.cs).

    - Add the using statement at the top of the file:

      ```csharp
      using Prometheus;
      ```

    - Add the following to the `Configure` method, after `app.UseRouting()`:

      ```csharp
      app.UseHttpMetrics();
      ```

    - Add the following to the `Configure` method, inside the `app.UseEndpoints`
      call:

      ```csharp
      endpoints.MapMetrics();
      ```

1.  Once the project has rebuilt, navigate to http://localhost:5000/metrics and
    take note of the output being generated by `prometheus-net`.

1.  Now make some requests to the the
    [`api/classrooms/{id}/students`](http://localhost:5000/api/classrooms/2ae08889-59d0-4d2a-920a-083ca2dba1a7/students)
    endpoint and then check to see how the output has changed.

1.  You may or may not have noticed that 500 errors are not present in the
    metrics. We've run into a subtle gotcha here. We need to change up the
    ordering of the code in the
    [`src/Imagine.Students.Api/Startup.cs`](src/Imagine.Students.Api/Startup.cs)
    to fix this. The `app.UseHttpMetrics()` call needs to come before the
    exception page call, `app.UseDeveloperExceptionPage()`. Let's move things
    around a little bit:

    ```csharp
    app.UseRouting();
    app.UseHttpMetrics();

    if (env.IsDevelopment())
    {
    	app.UseDeveloperExceptionPage();
    	app.UseSwagger();
    	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Imagine.Students.Api v1"));
    }
    ```

1.  Generate a few more requests and ensure that 500 errors are now showing up.

### Adding a Custom Metric ([see changes](https://github.com/dougludlow/metrics-workshop/commit/2661ae217d6ea4e1abcf55f67b77b528e1ce5415))

There are four main
[metric types](https://prometheus.io/docs/concepts/metric_types/) available in
Prometheus. These can be used to track custom metrics in your application, in
addition to the default ones provided by `prometheus-net`. The metric types are:

- Counter - for tracking values that increase.
- Gauge - for tracking values that increase or decrease.
- Histogram - samples observations and stores them in configurable buckets.
- Summary - is like a Histogram, but also provides a total count and sum of
  values in addition to calculating quantiles.

For example, you may want to track how long it takes to make queries to a
database or you may want to track how long HTTP requests to an external service
are taking. A Histogram or Summary would be good for those examples. Perhaps you
want to track how many users are logging in to the system or maybe you'd like to
track the number of active users. Counters or Gauges would be good to use in
those cases.

Let's use a Histogram to track how long it takes to retrieve students from the
store used by the API. We're going to be using the decorator pattern to wrap the
call to the student store. This will allow us to add our desired functionality
without having to modify the store's internals. This comes in handy for cases
where a datastore's code is from a library or SDK that we don't own or have
access to.

1. Now, let's add a `TrackedStudentsStore.cs` to the
   `src/Imagine.Students.Api/Services` folder, with the following code:

   ```csharp
   using System;
   using System.Collections.Generic;
   using System.Diagnostics;
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
   ```

   Don't forget to `await` the call to `GetStudents` or the numbers won't be
   quite right.

1. Next, we'll register the new decorator by adding the following to the bottom
   of the `ConfigureServices` method in the
   [`src/Imagine.Students.Api/Startup.cs`](src/Imagine.Students.Api/Startup.cs):

   ```csharp
   services.Decorate<IStudentsStore, TrackedStudentsStore>();
   ```

   **NOTE:** The [`Scrutor`](https://github.com/khellang/Scrutor) library is
   providing the `Decorate` method here.

1. Make a request to the
   [`api/classrooms/{id}/students`](http://localhost:5000/api/classrooms/2ae08889-59d0-4d2a-920a-083ca2dba1a7/students)
   endpoint and then take at look at the metrics endpoint
   (http://localhost:5000/metrics). You should now see our new metric somewhere
   in the response, something like:

   ```go
   # HELP student_api_operation_duration_seconds student_api_operation_duration_seconds
   # TYPE student_api_operation_duration_seconds histogram
   student_api_operation_duration_seconds_sum{class="TrackedStudentsStore",method="GetStudents"} 6.253066
   student_api_operation_duration_seconds_count{class="TrackedStudentsStore",method="GetStudents"} 1
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.005"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.01"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.025"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.05"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.075"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.1"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.25"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.5"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="0.75"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="1"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="2.5"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="5"} 0
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="7.5"} 1
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="10"} 1
   student_api_operation_duration_seconds_bucket{class="TrackedStudentsStore",method="GetStudents",le="+Inf"} 1
   ```

### Configuring Prometheus to Pull Metrics ([see changes](https://github.com/dougludlow/metrics-workshop/commit/8d371f968b9a55e11ec0d4f112b0dc9421b2772a))

Our API is now instrumented to return metrics, but Prometheus isn't aware of it
yet. We need to configure Prometheus to start scraping metrics from the API.

1. Open up the Prometheus config file
   [`config/prometheus/prometheus.yml`](config/prometheus/prometheus.yml) and
   add the following entry to the `scrape_configs` property:

   ```yaml
   - job_name: 'api'
     scrape_interval: 5s
     static_configs:
       - targets: ['api:5000']
   ```

1. Restart the `prometheus` service:

   ```bash
   docker-compose restart prometheus
   ```

1. Now navigate to http://localhost:9090. This is the Prometheus UI where you
   can make ad-hoc queries using
   [PromQL](https://prometheus.io/docs/prometheus/latest/querying/basics/).

1. Enter `http_requests_received_total` into the Expression field and hit
   Execute. Note the information that is returned.

1. Now enter `sum(http_requests_received_total)` and click on the Graph tab.
   Note how the data is represented over time.

1. We can also query our custom metric. This would give us the average duration:

   ```go
   student_api_operation_duration_seconds_sum / student_api_operation_duration_seconds_count
   ```

### Populating Some Metrics

Our application is running and metrics are being scraped, but the API isn't
getting any requests. Let's use `k6` to simulate some load.

1. Run the following command to run our `k6` script:

   ```bash
   docker-compose run --rm k6 run -e BASE_URL=http://api:5000 tests/load/classroom-students-constant.js
   ```

   This will simulate 1 user making 1 request to the
   `api/classrooms/{id}/students` endpoint each second for the next 60 minutes.
   Feel free to take a look at the
   [script](tests/load/classroom-students-constant.js) and make any adjustments
   you'd like (for instance, you can try making it simulate 5 users).

### Creating a Dashboard in Grafana ([see changes](https://github.com/dougludlow/metrics-workshop/commit/7c59b2fac0adb53fe2e05aa66351882cc0219cad))

Now we're finally ready to create a Grafana dashboard. We'll be adding some
graphs around request duration and error rates. We'll also add a graph around
our custom metric. We'll even add a request duration
[Apdex](https://en.wikipedia.org/wiki/Apdex) as a bonus.

1. First, let's navigate to http://localhost:3000 and log in. Use the following
   credentials:

   - Username: `admin`
   - Password: `foobar`

1. Next, hover over the
   <image src="docs/images/plus.png" alt="plus sign" height="20" style="height: 1.5rem;vertical-align: middle;" />
   in the left navigation and click
   "[Create](http://localhost:3000/dashboard/new)". Now, we can start adding
   panels.

#### Adding the Request Duration Panel

1. Click the
   <image src="docs/images/add-panel.png" alt="Add panel" height="20" style="height: 1.5rem;vertical-align: middle;" />
   button.

1. Click the "Add an empty panel" button in the new panel container.

1. Enter `Request Duration` in the "Panel title".

1. Expand the "Axes" section. Under "Left Y":

   - Select `Time / milliseconds (ms)` from the "Unit" dropdown.
   - Input `0` in "Y-Min".

1. On the "Query" tab, set "Metrics" to:

   ```go
   histogram_quantile(.99, sum by (le) (rate(http_request_duration_seconds_bucket{job="api"}[5m]))) * 1000
   ```

   This will show the average request duration that the top 1 percent of users
   are experiencing.

1. In the "Legend" field, enter `99th percentile`.

1. Click the
   <image src="docs/images/duplicate-query.png" alt="Duplicate query" height="20" style="height: 1.5rem;vertical-align: middle;" />
   button to make a copy of the first query.

1. Enter the following in the "Metrics" field, on query "B":

   ```go
   histogram_quantile(.95, sum by (le) (rate(http_request_duration_seconds_bucket{job="api"}[5m]))) * 1000
   ```

1. Change query B's "Legend" to `95th percentile`.

1. Duplicate
   (<image src="docs/images/duplicate-query.png" alt="Duplicate query" height="20" style="height: 1.5rem;vertical-align: middle;" />
   ) query B.

1. Modify query C's "Metrics" to be:

   ```go
   histogram_quantile(.75, sum by (le) (rate(http_request_duration_seconds_bucket{job="api"}[5m]))) * 1000
   ```

1. Update query C's "Legend" to be `75th percentile`.

1. Clone
   (<image src="docs/images/duplicate-query.png" alt="Duplicate query" height="20" style="height: 1.5rem;vertical-align: middle;" />
   ) query C.

1. Update query D's "Metrics" to:

   ```go
   histogram_quantile(.5, sum by (le) (rate(http_request_duration_seconds_bucket{job="api"}[5m]))) * 1000
   ```

1. Update query D's "Legend" to be `50th percentile`.

1. Finally, click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

#### Adding the Error Rate Panel

1. Click
   <image src="docs/images/add-panel.png" alt="Add panel" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

1. In the new panel container, click "Add an empty panel".

1. Enter `Error Rate` in the "Panel title".

1. Expand the "Display" section and, under "Stacking and null value", set "Null
   value" to `null as zero`.

1. Expand the "Axes" section and under "Left Y":

   - Set "Unit" to `Misc / Percent (0.0-1.0)`.
   - Set "Y-Min" to `0`.
   - Set "Y-Max" to `1`.

1. On the "Query" tab, set "Metrics" to the following:

   ```go
   sum(rate(http_requests_received_total{job="api",code=~"[45].."}[1m])) / sum(rate(http_requests_received_total{job="api"}[1m]))
   ```

1. In the "Legend" field, enter `Errors`.

1. Now click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

#### Adding the Requests Panel

1. Click
   <image src="docs/images/add-panel.png" alt="Add panel" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

1. Click "Add an empty panel".

1. Enter `Requests (per second)` in the "Panel title".

1. Expand the "Display" section and, under "Stacking and null value":

   - Set "Null value" to `null as zero`.

1. Inside the "Axes" section, enter `0` into the "Y-Min" field, under "Left Y".

1. On the "Query" tab, enter the following into the "Metrics" field:

   ```go
   sum by (code) (rate(http_requests_received_total{job="api"}[1m]))
   ```

1. In the "Legend" field, enter `{{code}}`. This will show the unique HTTP error
   status codes, if any.

1. Click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

#### Adding the Request Duration Apdex Panel

1. Click
   <image src="docs/images/add-panel.png" alt="Add panel" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

1. Click "Add an empty panel".

1. Enter `Request Duration Apdex` in the "Panel title".

1. Expand the "Axes" section and under "Left Y":

   - Set "Y-Min" to `0`.
   - Set "Y-Max" to `1`.

1. On the "Query" tab, enter the following into the "Metrics" field:

   ```go
   (
     sum(rate(http_request_duration_seconds_bucket{job="api",le="0.256",code!~"[45].."}[1m]))
     +
     sum(rate(http_request_duration_seconds_bucket{job="api",le="1.024",code!~"[45].."}[1m]))
   ) / 2 / sum(rate(http_request_duration_seconds_count[1m])) > 0 or vector(1)
   ```

1. In the "Legend" field, enter `Apdex`.

1. Click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

#### Adding the Operation Duration Panel

Let's add one last graph to track the custom metric we made earlier.

1. Click
   <image src="docs/images/add-panel.png" alt="Add panel" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

1. Click "Add an empty panel".

1. Enter `Operation Breakdown` in the "Panel title".

1. Expand the "Axes" section. Under "Left Y":

   - Select `Time / milliseconds (ms)` from the "Unit" dropdown.
   - Input `0` in "Y-Min".

1. On the "Query" tab, enter the following into the "Metrics" field:

   ```go
   sum by(class, method) (student_api_operation_duration_seconds_sum / student_api_operation_duration_seconds_count * 1000) > 0
   ```

1. In the "Legend" field, enter `{{class}}#{{method}}`.

1. Duplicate
   (<image src="docs/images/duplicate-query.png" alt="Duplicate query" height="20" style="height: 1.5rem;vertical-align: middle;" />
   ) query A.

1. Modify query B's "Metrics" to be:

   ```go
   http_request_duration_seconds_sum{code="200"} / http_request_duration_seconds_count{code="200"} * 1000
   ```

1. Update query C's "Legend" to be `Request Duration`.

1. Click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

#### Saving the Dashboard

We've done it, we've created a dashboard. Let's go ahead and save it.

1. First let's make the crosshair, that we hover over the panels with, shared:

   - Click the
     <image src="docs/images/dashboard-settings.png" alt="Dashboard settings" height="20" style="height: 1.5rem;vertical-align: middle;" />
     cog.
   - In "General", set the "Graph Tooltip" under "Panel Options" to `Shared
     crosshair".
   - Then, click
     <image src="docs/images/back.png" alt="Go back (Esc)" height="20" style="height: 1.5rem;vertical-align: middle;" />
     .

1. Click the
   <image src="docs/images/save-dashboard.png" alt="Save dashboard" height="20" style="height: 1.5rem;vertical-align: middle;" />
   button in top right.

1. Enter a name, something like `Students API`.

1. Click
   <image src="docs/images/save.png" alt="Save" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

### Adding Alerts ([see changes](https://github.com/dougludlow/metrics-workshop/commit/3ea28ef2f83315f51757f9221ff569754cc6c78a))

We're now ready to add an alert. We could alert on all sorts of things. Memory
usage being too high, CPU percentage being over a threshold for too long, etc.
Let's create an alert around our Request Duration Apdex.

1. Select the little chevron icon on the top of the "Request Duration Apdex"
   panel, then choose "Edit".

1. Click on the "Alert" tab and click on the "Create Alert" button.

1. Configure the following:

   - Set "Evaluate every" to `1m`
   - Set "For" to `1m`
   - Under "Conditions"
     - Set "When" to `avg()`
   - Set "Of" to `query (A, 1m, now)`
   - Select "Is Above" from the dropdown and change it to "Is Below"
   - Enter `.8`

   **NOTE:** In a production environment, we might configure services like Slack
   or Ops Genie to be notified when an alert triggers. We won't be setting up
   notifications for this demo.

1. Click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   .

1. Now, click the
   <image src="docs/images/save-dashboard.png" alt="Save dashboard" height="20" style="height: 1.5rem;vertical-align: middle;" />
   button in top right.

1. Let's run a simple load test with `k6`. You should see the Apdex alert tigger
   after the Apdex dives under .8 for a minute.

   ```bash
   docker-compose run --rm k6 run -e BASE_URL=http://api:5000 tests/load/classroom-students-stress.js
   ```

1. Let's fix the API by commenting out the `BrokenStudentsStore` decorator in
   the `src/Imagine.Students.Api/Startup.cs` file.

   ```csharp
   // services.Decorate<IStudentsStore, BrokenStudentsStore>();
   ```

1. Run `k6` again and notice that the alert clears after a minute.

### Extra Credit

If you've made it this far and have some extra time, feel free to add the
following panels to track CPU and memory usage.

**NOTE**: `cadvisor` is used to track CPU and memory metrics on Docker
containers. It may not work on Windows. If this is the case, the CPU and memory
panels we're creating now will be empty. Feel free to skip over these steps if
necessary.

#### Adding the CPU Usage Panel

1. Click "Add an empty panel".

1. On the "Panel" tab to the right, enter `CPU Usage` into the "Panel title"
   field.

1. Expand the "Display" section and, under "Stacking and null value", set "Null
   value" to `connected`.

1. Expand the "Axes" section below. From the "Unit" dropdown under "Left Y",
   choose `Misc / Percent (0-100)`.

1. On the "Query" tab at the bottom, enter the following in the "Metrics" field:

   ```go
   sum by (id) (rate(container_cpu_usage_seconds_total{container_label_com_docker_compose_service="api"}[2m]))
   /
   sum by (id) (
     container_spec_cpu_shares{container_label_com_docker_compose_service="api"}
     /
     container_spec_cpu_period{container_label_com_docker_compose_service="api"}
   )
   ```

1. In the "Legend" field just below, enter `{{id}}`. This will display the
   unique docker container IDs.

1. Click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   in the top right.

#### Adding the Memory Usage Panel

1. Click the
   <image src="docs/images/add-panel.png" alt="Add panel" height="20" style="height: 1.5rem;vertical-align: middle;" />
   button in the top right.

1. Click "Add an empty panel".

1. In the "Panel title" field, on the "Panel" tab to the right, enter
   `Memory Usage`.

1. Expand the "Display" section and, under "Stacking and null value", set "Null
   value" to `connected`.

1. Expand the "Axes" section and choose `Data / bytes(SI)` from the dropdown for
   in the "Unit".

1. On the "Query" tab, at the bottom, enter the following in the "Metrics"
   field:

   ```go
   sum by (id) (container_memory_working_set_bytes{container_label_com_docker_compose_service="api"})
   ```

1. In the "Legend" field, enter `{{id}}`.

1. Once again, click
   <image src="docs/images/apply.png" alt="Apply" height="20" style="height: 1.5rem;vertical-align: middle;" />
   in the top right.

## Conclusion

And that's it. Prometheus is a great tool for tracking metrics around your
distributed services. When paired with Grafana, you can very effectively monitor
your service's health and debug performance related issues. Alerts can help us
be proactive at providing better experiences for our users.

I hope you found this tutorial helpful. Let me know if I missed anything. Feel
free to open a pull request.

## Helpful Commands

- To clean the `prometheus` and `grafana` services' state, remove their data
  volumes:

  ```bash
  docker volume rm metrics-workshop_prometheus_data metrics-workshop_grafana_data
  ```

  **NOTE:** This will need to be run when the services are down. Run the
  following to take it all down:

  ```bash
  docker-compose down
  ```
