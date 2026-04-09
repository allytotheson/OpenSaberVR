# Triangulate all meshes in an FBX (fixes Unity "self-intersecting polygon discarded" when caused by bad quads/ngons).
# Requires Blender 3.x+ on PATH. Example:
#   blender --background --python Tools/triangulate_sword_fbx_blender.py -- "Assets/_Models/LeftSaber/LeftSword/source/Sword_01 (1).fbx" "Sword_01_fixed.fbx"
# Then replace the original FBX in the project with the output (keep the same .meta).

import sys

import bmesh
import bpy


def main() -> None:
    if "--" not in sys.argv:
        print("Usage: blender --background --python triangulate_sword_fbx_blender.py -- <input.fbx> <output.fbx>")
        sys.exit(1)
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) < 2:
        print("Need input and output FBX paths.")
        sys.exit(1)
    path_in, path_out = args[0], args[1]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path_in)

    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.to_mesh(mesh)
        mesh.update()
        bm.free()

    bpy.ops.export_scene.fbx(filepath=path_out, use_selection=False)


if __name__ == "__main__":
    main()
