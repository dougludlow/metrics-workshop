# Overview of Prometheus

Prometheus is an open-source systems monitoring and alerting toolkit, originally
built at SoundCloud. It is now a standalone solution and is part of the Cloud
Native Computing Foundation. It collects metrics about your applications and
infrastructure using an HTTP pull model. Grafana can be used to query the
Prometheus data to show in graphs on a dashboard. Alerts can also be configured
to send notifications when specified thresholds are reached.

## Case Studies

- TODO: Add a summary and screenshots around the recent math incident where the
  transaction trace feature helped track down an issue.
- TODO: Add a summary and screenshots about the issue that came up from the
  recent Rails 5 upgrade that caused rostering exports/backups to fail and how
  we used Grafana to discover a memory issue that was killing the background job
  pods.
