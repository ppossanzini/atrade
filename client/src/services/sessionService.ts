import { BaseRestService } from './baseRestService'

export class SessionService extends BaseRestService {
  constructor() {
    super('/api/session')
  }

  async login(request: server.LoginRequest): Promise<server.AuthenticatedSession> {
    return this.post<server.AuthenticatedSession>('/login', request)
  }

  async getCurrent(): Promise<server.Session> {
    return this.get<server.Session>('')
  }

  async logout(): Promise<void> {
    await this.post<void>('/logout', {})
  }
}

export const sessionService = new SessionService()
