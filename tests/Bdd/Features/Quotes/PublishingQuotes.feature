Feature: Publishing quotes
  Maintainers add quotes to the catalog. The catalog rejects near-duplicates by
  fingerprint, so punctuation and casing cannot be used to smuggle the same quote in twice.

  Background:
    Given the distributed application is running
    And I am signed in as "jrb"

  Scenario: A maintainer publishes a new quote
    When I publish a quote with unique text attributed to "Specification Suite"
    Then the response status is 201
    And the response carries a Location header
    And fetching that location returns the quote I published

  Scenario: A near-duplicate is rejected
    Given I have published a quote with unique text attributed to "Specification Suite"
    When I publish the same text with the final period replaced by an exclamation mark
    Then the response status is 409
    And the problem errorCode is "quote.duplicate_fingerprint"

  Scenario: Text that breaks the catalog rules is rejected
    When I publish a quote with the text "short"
    Then the response status is 400
    And the response is a problem document
