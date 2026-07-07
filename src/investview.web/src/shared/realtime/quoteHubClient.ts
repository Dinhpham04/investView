import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';

export const quoteHubPath = '/hubs/quotes';

export function createQuoteHubConnection(hubUrl = quoteHubPath): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export function isHubConnected(connection: HubConnection | null) {
  return connection?.state === HubConnectionState.Connected;
}
