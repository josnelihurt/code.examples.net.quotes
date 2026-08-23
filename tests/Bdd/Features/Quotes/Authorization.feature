Feature: Authorization
  The Quotes API admits callers by scope. Anonymous callers are challenged; readers may
  read but not publish; maintainers may do both.

  Background:
    Given the distributed application is running

  Scenario: An anonymous caller is challenged
    When I request a random quote from "v1"
    Then the response status is 401
    And the response carries a WWW-Authenticate header

  Scenario: A reader can read but not publish
    Given I am signed in as "reader"
    When I request a random quote from "v1"
    Then the response status is 200
    When I publish a quote with unique text attributed to "Reader Should Not Publish"
    Then the response status is 403

  Scenario: A maintainer can publish
    Given I am signed in as "jrb"
    When I publish a quote with unique text attributed to "Specification Suite"
    Then the response status is 201
