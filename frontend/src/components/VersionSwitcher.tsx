import { API_VERSIONS, type ApiVersion } from '../api/client';

interface VersionSwitcherProps {
  version: ApiVersion;
  onChange: (next: ApiVersion) => void;
}

/**
 * Chooses which transport serves the quote use cases: v0 by MVC controllers, v1 by
 * minimal APIs. The radio ids are part of the E2E vocabulary (steps target #version-v0/v1).
 */
export function VersionSwitcher({ version, onChange }: Readonly<VersionSwitcherProps>) {
  return (
    <fieldset className="versions">
      <legend>API version</legend>
      {API_VERSIONS.map((option) => (
        <label key={option} htmlFor={`version-${option}`}>
          <input
            type="radio"
            id={`version-${option}`}
            name="apiVersion"
            value={option}
            checked={version === option}
            onChange={() => onChange(option)}
          />
          {option === 'v0' ? `${option} (controllers)` : `${option} (minimal APIs)`}
        </label>
      ))}
    </fieldset>
  );
}
