Feature: API documentation
  Every API publishes its OpenAPI document and a Scalar reference page. They are part of
  the contract surface: if they disappear, clients and tooling break. The gateway only
  routes the /api prefixes, so these surfaces are addressed on the services themselves.

  Background:
    Given the distributed application is running

  Scenario: The Auth API publishes its reference surfaces
    When I open "/scalar/" on the "auth-api" service
    Then the response status is 200
    When I open "/openapi/v1.json" on the "auth-api" service
    Then the response status is 200

  Scenario: The Quotes API publishes one document per transport version
    When I open "/scalar/" on the "quotes-api" service
    Then the response status is 200
    When I open "/openapi/v0.json" on the "quotes-api" service
    Then the response status is 200
    When I open "/openapi/v1.json" on the "quotes-api" service
    Then the response status is 200
    When I open "/openapi/v2.json" on the "quotes-api" service
    Then the response status is 200

  Scenario: The transcoded transport serves the OpenAPI document generated from its proto
    Transcoded routes are invisible to ApiExplorer, so no runtime document exists; the
    freeze pipeline generates one from the contract itself and the API serves it verbatim.
    When I open "/openapi/v3.json" on the "quotes-api" service
    Then the response status is 200
