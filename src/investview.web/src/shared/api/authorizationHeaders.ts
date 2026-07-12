export function authorizationHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}
