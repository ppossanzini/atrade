import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios'
import { getSettings } from '@/settings'

/**
 * Supplies transport headers for the current request. The session store registers a provider that
 * adds the antiforgery header, so service classes stay free of authentication concerns.
 */
export type RequestHeaderProvider = () => Record<string, string>

let requestHeaderProvider: RequestHeaderProvider | null = null

export function setRequestHeaderProvider(provider: RequestHeaderProvider | null): void {
  requestHeaderProvider = provider
}

/**
 * Returns the HTTP status of a failed request, or null when the failure was not an HTTP response.
 * Keeps the HTTP client confined to the service layer.
 */
export function getHttpStatus(error: unknown): number | null {
  return axios.isAxiosError(error) && error.response ? error.response.status : null
}

/**
 * Returns the response body of a failed request, or null when there is none. A refused command answers with
 * the same contract as an accepted one, so the reason it was refused travels in the body and must not be
 * thrown away with the status.
 */
export function getHttpErrorBody<TResponse>(error: unknown): TResponse | null {
  return axios.isAxiosError(error) && error.response
    ? (error.response.data as TResponse)
    : null
}

/**
 * Shared transport base for domain services. It centralizes the axios instance, the runtime base
 * URL and the typed helpers; it contains no endpoint-specific logic.
 */
export abstract class BaseRestService {
  private readonly basePath: string

  private readonly client: AxiosInstance

  protected constructor(basePath: string) {
    this.basePath = basePath
    this.client = axios.create({ withCredentials: true })
  }

  protected get<TResponse>(path: string): Promise<TResponse> {
    return this.send<TResponse>({ method: 'GET', url: this.resolve(path) })
  }

  protected post<TResponse>(path: string, body: unknown): Promise<TResponse> {
    return this.send<TResponse>({ method: 'POST', url: this.resolve(path), data: body })
  }

  protected patch<TResponse>(path: string, body: unknown): Promise<TResponse> {
    return this.send<TResponse>({ method: 'PATCH', url: this.resolve(path), data: body })
  }

  protected put<TResponse>(path: string, body: unknown): Promise<TResponse> {
    return this.send<TResponse>({ method: 'PUT', url: this.resolve(path), data: body })
  }

  protected delete<TResponse>(path: string): Promise<TResponse> {
    return this.send<TResponse>({ method: 'DELETE', url: this.resolve(path) })
  }

  /**
   * Resolves the URL per request so a runtime settings override applies even when the settings
   * file is loaded after the service instances were created.
   */
  private resolve(path: string): string {
    return `${getSettings().apiBaseUrl}${this.basePath}${path}`
  }

  private async send<TResponse>(config: AxiosRequestConfig): Promise<TResponse> {
    const providerHeaders = requestHeaderProvider ? requestHeaderProvider() : {}

    const response = await this.client.request<TResponse>({
      ...config,
      headers: { ...providerHeaders, ...config.headers },
    })

    return response.data
  }
}
