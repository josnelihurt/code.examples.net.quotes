Feature: Signing in
  Local users exchange their credentials for an access token at the Auth API, reached
  through the gateway. The token carries the scopes that decide what they may do at the
  Quotes API.

  Scenario: A maintainer signs in and reads a random quote through the gateway
    Given the distributed application is running
    When I sign in as "jrb" with password "supersecret"
    Then the response status is 200
    And the response body has "accessToken"
    When I request a random quote from "v1"
    Then the response status is 200
    And the response body has "text" and "author"
