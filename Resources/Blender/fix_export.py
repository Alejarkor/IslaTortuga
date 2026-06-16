"""
Arreglo para exportar a glTF en Blender 4.4 (Isla Tortuga).

CAUSA REAL (según el traceback del exportador):
  io_scene_gltf2 .../animation/action.py -> __get_blender_actions:
      new_action.add_slot(strip.action_slot, strip.action_slot.target_id_type, ...)
      AttributeError: 'NoneType' object has no attribute 'target_id_type'
  => Hay strips en pistas NLA cuyo `action_slot` es None (acciones por "slots"
     de Blender 4.4 mal vinculadas). El exporter peta al leerlas.

Este script:
  1) Repara las pistas NLA: a cada strip con acción pero sin action_slot le
     asigna un slot válido de su acción; si no se puede, silencia (mute) el strip.
  2) Quita la animation_data de las MALLAS (las acciones de huesos solo deben
     vivir en el Armature).
  3) Borra el objeto sobrante "IT_Character" y su malla huérfana.
  4) Marca las acciones con fake user y purga huérfanos.

USO: Scripting -> Open -> fix_export.py -> Run. GUARDA UNA COPIA antes.
Luego exporta glTF (Animation -> Mode: "Actions").

NOTA: este crash es un bug del add-on glTF de 4.4 ya corregido en versiones
posteriores. Si tras el script siguiera fallando, ACTUALIZA Blender a la última
4.4.x / 4.5 (la vía más limpia) o, como atajo, exporta con Animation Mode
"Active action" si solo necesitas la animación activa.
"""
import bpy

OLD_OBJECT_NAME = "IT_Character"

# ------------------------------------------------------------------ #
# 1) Reparar slots nulos en strips NLA (la causa del crash)
# ------------------------------------------------------------------ #
def first_compatible_slot(action, owner):
    """Devuelve un slot del action o crea uno; None si no es posible."""
    slots = getattr(action, "slots", None)
    if slots is None:
        return None
    if len(slots) > 0:
        return slots[0]
    # Crear un slot nuevo (la firma varía entre 4.4.x; probamos variantes).
    for attempt in (
        lambda: slots.new(id_type='OBJECT', name="Legacy"),
        lambda: slots.new('OBJECT', "Legacy"),
        lambda: slots.new(),
    ):
        try:
            return attempt()
        except Exception:
            continue
    return None

fixed, muted = 0, 0
for obj in bpy.data.objects:
    ad = obj.animation_data
    if not ad:
        continue
    for track in ad.nla_tracks:
        for strip in track.strips:
            if getattr(strip, "type", "CLIP") != 'CLIP':
                continue
            action = getattr(strip, "action", None)
            if action is None:
                continue
            if getattr(strip, "action_slot", "missing") is None:
                slot = first_compatible_slot(action, obj)
                if slot is not None:
                    try:
                        strip.action_slot = slot
                        fixed += 1
                        continue
                    except Exception:
                        pass
                strip.mute = True
                muted += 1
print(f"[fix] NLA: {fixed} slots reparados, {muted} strips silenciados")

# ------------------------------------------------------------------ #
# 2) Quitar animation_data de las mallas
# ------------------------------------------------------------------ #
cleared = 0
for ob in bpy.data.objects:
    if ob.type == 'MESH' and ob.animation_data is not None:
        ob.animation_data_clear()
        cleared += 1
print(f"[fix] animation_data limpiada en {cleared} mallas")

# ------------------------------------------------------------------ #
# 3) Borrar el personaje sobrante + malla huérfana
# ------------------------------------------------------------------ #
old = bpy.data.objects.get(OLD_OBJECT_NAME)
if old is not None:
    mesh = old.data if old.type == 'MESH' else None
    bpy.data.objects.remove(old, do_unlink=True)
    print(f"[fix] objeto '{OLD_OBJECT_NAME}' eliminado")
    if mesh is not None and mesh.users == 0:
        bpy.data.meshes.remove(mesh)
        print("[fix] malla huérfana eliminada")
else:
    print(f"[fix] no se encontró '{OLD_OBJECT_NAME}' (¿ya borrado?)")

# ------------------------------------------------------------------ #
# 4) Conservar acciones + purga
# ------------------------------------------------------------------ #
for act in bpy.data.actions:
    act.use_fake_user = True
print(f"[fix] {len(bpy.data.actions)} acciones con fake user")

try:
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True,
                                   do_recursive=True)
    print("[fix] purga de huérfanos completada")
except Exception as e:
    print("[fix] purga manual (File > Clean Up > Purge All):", e)

print("[fix] LISTO. Exporta glTF con Animation -> Mode: 'Actions'.")
