Feature: Signing in
  The SPA gates the quote reader behind local credentials. Wrong credentials must be
  explained inline, and the quote page must not render for anonymous visitors.

  Scenario: Valid credentials reach the quote page
    Given I am on the sign-in page
    When I sign in as "jrb" with password "supersecret"
    Then I reach the quote page

  Scenario: Invalid credentials surface an error and stay put
    Given I am on the sign-in page
    When I sign in as "jrb" with password "wrong-password"
    Then an alert explains the problem
    And I stay on the sign-in page

  Scenario: Visiting the quote page unauthenticated redirects to sign-in
    When I visit "/quote"
    Then I stay on the sign-in page
