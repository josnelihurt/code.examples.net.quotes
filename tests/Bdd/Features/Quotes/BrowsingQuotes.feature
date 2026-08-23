Feature: Browsing quotes
  Readers pull quotes from the catalog: a random one, one by id, or a page of the stable
  ordering. The catalog ships seeded, so there is always something to read.

  Background:
    Given the distributed application is running
    And I am signed in as "jrb"

  Scenario: A random quote comes back with its text and author
    When I request a random quote from "v1"
    Then the response status is 200
    And the response body has "text" and "author"
    And the X-Correlation-Id header is echoed

  Scenario: A quote can be fetched again by its id
    Given I have published a quote with unique text attributed to "Specification Suite"
    When I request the quote I published from "v1"
    Then the response status is 200
    And the response body is the quote I published

  Scenario: An unknown id is a clean 404
    When I request the quote with id "00000000000000000000000000000000" from "v1"
    Then the response status is 404
    And the problem errorCode is "quote.not_found"

  Scenario: Listing without parameters honors the default paging
    When I list quotes from "v1"
    Then the response status is 200
    And the response reports page 1 with the default page size

  Scenario: A page request outside the allowed range is rejected
    When I list page 0 with size 10 from "v1"
    Then the response status is 400
    And the problem errorCode is "quote.invalid_page_request"
