import { BaseRestService } from './baseRestService'

export class BasketService extends BaseRestService {
  constructor() {
    super('/api/baskets')
  }

  async getBaskets(): Promise<server.BasketSummary[]> {
    return this.get<server.BasketSummary[]>('')
  }

  async getActiveBasket(): Promise<server.BasketDetail> {
    return this.get<server.BasketDetail>('/active')
  }

  async getBasketById(basketId: string): Promise<server.BasketDetail> {
    return this.get<server.BasketDetail>(`/${basketId}`)
  }

  async getVersions(basketId: string): Promise<server.BasketVersion[]> {
    return this.get<server.BasketVersion[]>(`/${basketId}/versions`)
  }

  async createBasket(request: server.BasketIdentity): Promise<server.BasketDetail> {
    return this.post<server.BasketDetail>('', request)
  }

  async cloneBasket(
    basketId: string,
    request: server.BasketIdentity,
  ): Promise<server.BasketDetail> {
    return this.post<server.BasketDetail>(`/${basketId}/clone`, request)
  }

  async updateIdentity(
    basketId: string,
    request: server.BasketIdentity,
  ): Promise<server.BasketDetail> {
    return this.patch<server.BasketDetail>(`/${basketId}/identity`, request)
  }

  async updateComposition(
    basketId: string,
    legs: server.BasketCompositionLeg[],
  ): Promise<server.BasketDetail> {
    return this.patch<server.BasketDetail>(`/${basketId}/composition`, legs)
  }

  async updatePolicy(basketId: string, policy: server.BasketPolicy): Promise<server.BasketDetail> {
    return this.patch<server.BasketDetail>(`/${basketId}/policy`, policy)
  }

  async publishVersion(
    basketId: string,
    request: server.BasketVersionPublication,
  ): Promise<server.BasketDetail> {
    return this.post<server.BasketDetail>(`/${basketId}/versions`, request)
  }

  async activateVersion(basketId: string, versionId: string): Promise<server.BasketDetail> {
    return this.post<server.BasketDetail>(`/${basketId}/versions/${versionId}/activate`, {})
  }

  async archiveBasket(basketId: string): Promise<server.BasketDetail> {
    return this.post<server.BasketDetail>(`/${basketId}/archive`, {})
  }
}

export const basketService = new BasketService()
