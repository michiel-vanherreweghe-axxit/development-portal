import { useSession } from "./auth/useSession";
import { Portal } from "./Portal";
import { Spinner } from "./ui/Spinner";

export function App() {
  const session = useSession();

  if (session.loading) {
    return (
      <div className="booting">
        <Spinner label="Loading the portal" />
      </div>
    );
  }

  // Remounting on connect or disconnect throws away whatever the previous account loaded.
  return <Portal key={session.user?.gitHubId ?? "anonymous"} session={session} />;
}
