/**
 * Runtime application settings.
 *
 * Defaults live here; deployment can override them without rebuilding through
 * `public/settings.json`, which is fetched once at startup.
 */
export interface AppSettings {
  apiBaseUrl: string
}

const defaultSettings: AppSettings = {
  apiBaseUrl: 'http://127.0.0.1:5271',
}

let currentSettings: AppSettings = { ...defaultSettings }

export function getSettings(): AppSettings {
  return currentSettings
}

/**
 * Loads the deployment override. A missing or unreadable file is not an error:
 * the built-in defaults stay in effect.
 */
export async function loadSettings(): Promise<AppSettings> {
  try {
    const response = await fetch(`${import.meta.env.BASE_URL}settings.json`, { cache: 'no-store' })

    if (!response.ok) {
      return currentSettings
    }

    const loadedSettings = (await response.json()) as Partial<AppSettings>
    currentSettings = { ...defaultSettings, ...loadedSettings }
  } catch {
    currentSettings = { ...defaultSettings }
  }

  return currentSettings
}
