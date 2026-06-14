import type { InternalRequestNote } from "@kermaria/shared";

import { EmptyState } from "@/components/EmptyState";
import { formatDateTime } from "@/lib/formatters";

type InternalNoteListProps = {
  notes: InternalRequestNote[];
};

export function InternalNoteList({ notes }: InternalNoteListProps) {
  if (notes.length === 0) {
    return (
      <EmptyState
        description="Aucune note interne n’a été ajoutée."
        title="Aucune note"
      />
    );
  }

  return (
    <div className="internal-note-list">
      {notes.map((note) => (
        <article className="internal-note" key={note.id}>
          <p>{note.note}</p>
          <footer>
            {note.authorDisplayName} · {formatDateTime(note.createdAt)}
          </footer>
        </article>
      ))}
    </div>
  );
}
