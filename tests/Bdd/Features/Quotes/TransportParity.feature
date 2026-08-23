Feature: Dual transport parity
  The same use cases are served twice: v0 by MVC controllers, v1 by minimal APIs.
  A caller must not be able to tell which one answered.

  Background:
    Given the distributed application is running
    And I am signed in as "jrb"

  Scenario Outline: A random quote is served identically by both transports
    When I request a random quote from "<version>"
    Then the response status is 200
    And the response body has "text" and "author"
    And the X-Correlation-Id header is echoed

    Examples:
      | version |
      | v0      |
      | v1      |
