import { authorizationHeaders } from './authorizationHeaders';
import { getJson, postJson } from './httpClient';
import type { PlaceOrderRequest, PortfolioSnapshot, SimulatedOrder } from '../types/trading';

export function getPortfolio(accessToken: string) {
  return getJson<PortfolioSnapshot>('/api/portfolio', {
    headers: authorizationHeaders(accessToken),
  });
}

export function getOrders(accessToken: string) {
  return getJson<SimulatedOrder[]>('/api/orders', {
    headers: authorizationHeaders(accessToken),
  });
}

export function placeOrder(accessToken: string, request: PlaceOrderRequest) {
  return postJson<SimulatedOrder, PlaceOrderRequest>('/api/orders', request, {
    headers: authorizationHeaders(accessToken),
  });
}

export function cancelOrder(accessToken: string, orderId: string) {
  return postJson<SimulatedOrder, null>(`/api/orders/${encodeURIComponent(orderId)}/cancel`, null, {
    headers: authorizationHeaders(accessToken),
  });
}
