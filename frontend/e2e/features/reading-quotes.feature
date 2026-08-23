Feature: Reading quotes
  A signed-in maintainer can pull a random quote and choose which transport version
  serves it. Signing out clears the session and returns to the sign-in page.

  Background:
    Given I am on the sign-in page
    And I sign in as "jrb" with password "supersecret"

  Scenario: A random quote is displayed
    When I fetch a random quote
    Then a quote is displayed
    And the quote was served by "v1"

  Scenario: The v0 transport serves the quote
    When I switch the API version to "v0"
    And I fetch a random quote
    Then the quote was served by "v0"

  Scenario: Signing out returns to sign-in
    When I sign out
    Then I stay on the sign-in page
