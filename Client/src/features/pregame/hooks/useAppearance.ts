import { useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { fetchAppearance, saveAppearance } from "@/api/appearance.api";
import { ApiError } from "@/api/httpClient";
import type { Appearance } from "@/domain/appearance/appearanceSchema";
import { NO_HAIR_ID } from "@/config/characterColorSlots";
import { useCharacterEditorStore } from "@/store/characterEditorStore";

const APPEARANCE_KEY = ["appearance"] as const;

/**
 * Carga la apariencia guardada y la vuelca en el store del editor.
 * Mantiene el patrón "estado guardado vs estado en edición".
 */
export function useLoadAppearance() {
  const initialize = useCharacterEditorStore((s) => s.initialize);

  const query = useQuery<Appearance>({
    queryKey: APPEARANCE_KEY,
    queryFn: ({ signal }) => fetchAppearance(signal)
  });

  useEffect(() => {
    if (query.data) {
      initialize(query.data);
    }
  }, [query.data, initialize]);

  return query;
}

/**
 * Mutación de guardado explícito.
 * Valida en cliente que el hair_id sea "Sin pelo" o uno de los estilos
 * descubiertos en el pack de pelo antes de enviar.
 */
export function useSaveAppearance(validHairIds: Set<string>) {
  const queryClient = useQueryClient();
  const beginSave = useCharacterEditorStore((s) => s.beginSave);
  const commitSaved = useCharacterEditorStore((s) => s.commitSaved);
  const failSave = useCharacterEditorStore((s) => s.failSave);

  return useMutation<Appearance, Error, Appearance>({
    mutationFn: async (editing) => {
      const validHair =
        editing.hair_id === NO_HAIR_ID || validHairIds.has(editing.hair_id);
      // Si aún no se han descubierto estilos, no bloqueamos el guardado.
      if (validHairIds.size > 0 && !validHair) {
        throw new Error("El pelo seleccionado no está disponible.");
      }
      return saveAppearance(editing);
    },
    onMutate: () => {
      beginSave();
    },
    onSuccess: (saved) => {
      commitSaved(saved);
      queryClient.setQueryData(APPEARANCE_KEY, saved);
    },
    onError: (error) => {
      const message =
        error instanceof ApiError
          ? error.message
          : error.message || "No se pudo guardar la apariencia.";
      failSave(message);
    }
  });
}
