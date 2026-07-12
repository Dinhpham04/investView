import { useContext } from 'react';
import { DemoSessionContext } from './demoSessionContext';

export function useDemoSession() {
  const value = useContext(DemoSessionContext);
  if (value == null) {
    throw new Error('useDemoSession must be used within DemoSessionProvider.');
  }

  return value;
}
