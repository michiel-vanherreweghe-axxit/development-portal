import type { ReactNode } from "react";

/**
 * Pages are declared as data: the sidebar, the hash route (#id) and the heading all follow
 * from this, so adding one needs no router.
 */
export interface PageDefinition {
  id: string;
  title: string;
  /** The sidebar heading this page sits under. */
  group?: string;
  hint: string;
  icon: ReactNode;
  badge?: number | null;
  action?: ReactNode;
  render: () => ReactNode;
}

export interface Command {
  group: string;
  label: string;
  hint?: string;
  icon?: ReactNode;
  run: () => void;
}
