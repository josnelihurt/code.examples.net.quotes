Feature: Publishing quotes
  A signed-in maintainer adds quotes to the catalog from the browser. Rule-breaking
  text and near-duplicates are explained inline, and read-only accounts cannot
  publish. The vocabulary mirrors the specification suite's PublishingQuotes and
  Authorization features — one business language across both BDD layers.

  Background:
    Given I am on the sign-in page
    And I sign in as "jrb" with password "supersecret"

  Scenario: A maintainer publishes a new quote
    When I fill the publish form with unique text attributed to "Browser Suite"
    And I submit the publish form
    Then the published quote is confirmed
    And I open the catalog
    And I move to the last page
    Then the catalog lists the quote I published

  Scenario: Text that breaks the catalog rules is explained inline
    When I fill the publish form with the text "short" attributed to "Browser Suite"
    And I submit the publish form
    Then an alert explains the problem

  Scenario: A near-duplicate is rejected as a conflict
    Given I have published a quote with unique text attributed to "Browser Suite"
    When I refill the publish form with the same text ending in an exclamation mark
    And I submit the publish form
    Then an alert explains the conflict

  Scenario: A reader cannot publish
    Given I am on the sign-in page
    And I sign in as "reader" with password "readsecret"
    When I fill the publish form with unique text attributed to "Browser Suite"
    And I submit the publish form
    Then an alert explains the missing write permission
