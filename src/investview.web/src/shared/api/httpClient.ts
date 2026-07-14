export class HttpError extends Error {
  readonly status: number;
  readonly statusText: string;

  constructor(status: number, statusText: string, message?: string) {
    super(message ?? `Request failed: ${status} ${statusText}`);
    this.name = 'HttpError';
    this.status = status;
    this.statusText = statusText;
  }
}

const unauthorizedSubscribers = new Set<() => void>();

export function subscribeToUnauthorized(subscriber: () => void) {
  unauthorizedSubscribers.add(subscriber);
  return () => {
    unauthorizedSubscribers.delete(subscriber);
  };
}

export async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await request(url, init);
  return response.json() as Promise<T>;
}

export async function postJson<TResponse, TBody>(url: string, body: TBody, init?: RequestInit): Promise<TResponse> {
  const response = await request(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
    method: 'POST',
    body: JSON.stringify(body),
  });

  return response.json() as Promise<TResponse>;
}

export async function deleteRequest(url: string, init?: RequestInit): Promise<void> {
  await request(url, {
    ...init,
    method: 'DELETE',
  });
}

async function request(url: string, init?: RequestInit): Promise<Response> {
  const response = await fetch(url, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const message = await readErrorMessage(response);

    if (response.status === 401) {
      for (const subscriber of unauthorizedSubscribers) {
        subscriber();
      }
    }

    throw new HttpError(response.status, response.statusText, message);
  }

  return response;
}

async function readErrorMessage(response: Response) {
  const fallback = `Request failed: ${response.status} ${response.statusText}`;
  const bodyText = await response.text().catch(() => '');
  const trimmedBody = bodyText.trim();

  if (trimmedBody.length === 0) {
    return fallback;
  }

  if (isJsonContentType(response.headers.get('Content-Type'))) {
    const problem = parseJsonObject(trimmedBody);
    const title = getStringProperty(problem, 'title');
    const detail = getStringProperty(problem, 'detail');

    if (title != null && detail != null && detail !== title) {
      return `${title}: ${detail}`;
    }

    if (title != null) {
      return title;
    }

    if (detail != null) {
      return detail;
    }
  }

  return trimmedBody;
}

function parseJsonObject(value: string): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(value);
    return typeof parsed === 'object' && parsed != null && !Array.isArray(parsed)
      ? parsed as Record<string, unknown>
      : null;
  } catch {
    return null;
  }
}

function isJsonContentType(contentType: string | null) {
  const normalizedContentType = contentType?.toLowerCase() ?? '';
  return normalizedContentType.includes('application/json') || normalizedContentType.includes('+json');
}

function getStringProperty(value: Record<string, unknown> | null, property: string) {
  const propertyValue = value?.[property];
  return typeof propertyValue === 'string' && propertyValue.trim().length > 0
    ? propertyValue.trim()
    : null;
}
