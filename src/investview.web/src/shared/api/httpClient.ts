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
    throw new Error(`Request failed: ${response.status} ${response.statusText}`);
  }

  return response;
}
