import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../lib/auth/AuthContext';
import type { ReactNode } from 'react';

interface Props {
  children: ReactNode;
  roles?: string[];
}

export default function ProtectedRoute({ children, roles }: Props) {
  const { status, user, hasRole } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return (
      <div className="grid min-h-screen place-items-center bg-zinc-50 text-sm text-zinc-500 dark:bg-zinc-950">
        A verificar sessão…
      </div>
    );
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (user?.requireChangePasswordOnNextLogin && location.pathname !== '/auth/change-password') {
    return <Navigate to="/auth/change-password" replace state={{ from: location }} />;
  }

  if (roles && !roles.some((r) => hasRole(r))) {
    return (
      <div className="grid min-h-[60vh] place-items-center text-sm text-zinc-500">
        Sem permissões para aceder.
      </div>
    );
  }

  return <>{children}</>;
}
