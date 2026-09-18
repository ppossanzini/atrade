import { BaseRestService } from './baseRestService'

export class SessionService extends BaseRestService {
  constructor() {
    super('/api/session')
  }

  async getAntiforgeryToken(): Promise<server.AntiforgeryToken> {
    return this.get<server.AntiforgeryToken>('/antiforgery-token')
  }

  async login(request: server.LoginRequest): Promise<server.Session> {
    return this.post<server.Session>('/login', request)
  }

  async getCurrent(): Promise<server.Session> {
    return this.get<server.Session>('')
  }

  async logout(): Promise<void> {
    await this.post<void>('/logout', {})
  }
}

export const sessionService = new SessionService()
