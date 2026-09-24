import bpy
import os
from mathutils import Vector

OUT = r"C:\Users\morda\.cursor\projects\Valheim Auto Storage und Craft mod\valheim mod\Content\Displays"

SIGNS = [
    ("small_vertical", "SAC_Small_Vertical"),
    ("small_horizontal", "SAC_Small_Horizontal"),
    ("medium_vertical", "SAC_Medium_Vertical"),
    ("medium_horizontal", "SAC_Medium_Horizontal"),
    ("large_vertical", "SAC_Large_Vertical"),
    ("large_horizontal", "SAC_Large_Horizontal"),
]

def unity(p):
    return (p.x, p.z, -p.y)

def mat_name(mesh_obj, index):
    if index < 0 or index >= len(mesh_obj.material_slots):
        return "mat"
    mat = mesh_obj.material_slots[index].material
    if mat is None:
        return "mat"
    return mat.name

def is_snow(name):
    return "snow" in name.lower()

def write_obj(path, obj, mw):
    mesh = obj.data
    mesh.calc_loop_triangles()
    lines = []
    verts = []
    for v in mesh.vertices:
        p = mw @ v.co
        u = unity(p)
        verts.append(u)
        lines.append("v %.6f %.6f %.6f" % u)
    norms = []
    nmat = mw.to_3x3()
    for v in mesh.vertices:
        n = (nmat @ v.normal)
        nu = Vector(unity(n))
        if nu.length > 0.0001:
            nu.normalize()
        norms.append(nu)
        lines.append("vn %.6f %.6f %.6f" % (nu.x, nu.y, nu.z))
    uvs = mesh.uv_layers.active.data if mesh.uv_layers.active else None
    if uvs:
        # one uv per loop would desync indices; use vertex uv from first loop
        uv_at = [None] * len(mesh.vertices)
        for loop in mesh.loops:
            if uv_at[loop.vertex_index] is None:
                uv_at[loop.vertex_index] = uvs[loop.index].uv
        for i in range(len(mesh.vertices)):
            uv = uv_at[i]
            if uv is None:
                lines.append("vt 0 0")
            else:
                lines.append("vt %.6f %.6f" % (uv.x, uv.y))
    else:
        for _ in mesh.vertices:
            lines.append("vt 0 0")

    by_mat = {}
    for tri in mesh.loop_triangles:
        name = mat_name(obj, tri.material_index)
        if is_snow(name):
            continue
        by_mat.setdefault(name, []).append(tri.vertices)

    if not by_mat:
        return False

    body = []
    for name, tris in by_mat.items():
        body.append("usemtl " + name)
        for a, b, c in tris:
            body.append("f %d/%d/%d %d/%d/%d %d/%d/%d" % (
                a + 1, a + 1, a + 1, b + 1, b + 1, b + 1, c + 1, c + 1, c + 1))
    text = "\n".join(lines + body) + "\n"
    with open(path, "w", encoding="ascii", errors="replace") as f:
        f.write(text)

    xs = [v[0] for v in verts]
    ys = [v[1] for v in verts]
    zs = [v[2] for v in verts]
    print(" ", os.path.basename(path), "verts", len(verts),
          "size", round(max(xs) - min(xs), 3), round(max(ys) - min(ys), 3), round(max(zs) - min(zs), 3),
          "mats", list(by_mat.keys()))
    return True

def descendants(root):
    out = []
    def walk(o):
        for c in bpy.data.objects:
            if c.parent == o:
                if c.type == "MESH":
                    out.append(c)
                walk(c)
    walk(root)
    return out

os.makedirs(OUT, exist_ok=True)
for folder, root_name in SIGNS:
    root = bpy.data.objects[root_name]
    dest = os.path.join(OUT, folder)
    os.makedirs(dest, exist_ok=True)
    for old in os.listdir(dest):
        if old.endswith(".obj"):
            os.remove(os.path.join(dest, old))
    inv = root.matrix_world.inverted()
    print("==", folder)
    n = 0
    for obj in descendants(root):
        mw = inv @ obj.matrix_world
        safe = "".join(ch if ch.isalnum() or ch in "._-" else "_" for ch in obj.name)
        path = os.path.join(dest, "%02d_%s.obj" % (n, safe))
        if write_obj(path, obj, mw):
            n += 1
        else:
            print("  skip", obj.name)
    print(" parts", n)
print("DONE", OUT)
