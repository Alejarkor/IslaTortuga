#!/usr/bin/env python3
"""Trocea la lamina de UI (ui_sheet_v1, 1536x1024) en assets PNG con alpha.
Coordenadas fijas por celda (lamina concreta). Fondo claro -> transparente,
relleno de interiores (pergaminos), PanelFrame hueco, ButtonTeal en 3 estados,
insets de 9-slice y hoja de contacto."""
import sys, os, json
import numpy as np
from PIL import Image
from scipy import ndimage

# (name, y0, y1, x0, x1) sobre la lamina 1536x1024
CELLS = [
    ("LogoIslaTortuga", 15, 399, 20, 266),
    ("LobbyBG",         15, 399, 314, 1169),
    ("PanelLogin",      15, 399, 1212, 1494),
    ("PanelFrame",      399, 560, 23, 312),
    ("PanelParchment",  399, 560, 373, 558),
    ("RopeTrim",        399, 560, 601, 772),
    ("CornerOrnament",  399, 560, 824, 944),
    ("HeaderFrame",     399, 560, 996, 1515),
    ("ButtonTeal",      576, 733, 26, 218),
    ("ButtonGold",      576, 733, 350, 561),
    ("ButtonPlay",      576, 733, 613, 907),
    ("IconButton",      576, 733, 951, 1064),
    ("ArrowButton",     576, 733, 1134, 1260),
    ("CloseButton",     576, 733, 1333, 1455),
    ("InputField",      757, 845, 27, 287),
    ("Dropdown",        757, 845, 351, 609),
    ("Checkbox",        757, 845, 658, 758),
    ("RadioOn",         757, 845, 813, 903),
    ("RadioOff",        757, 845, 963, 1055),
    ("Slider",          757, 845, 1134, 1493),
    ("CoinIcon",        880, 1015, 22, 118),
    ("GemIcon",         880, 1015, 159, 252),
    ("EnergyIcon",      880, 1015, 300, 409),
    ("ChestIcon",       880, 1015, 446, 550),
    ("MapIcon",         880, 1015, 591, 695),
    ("RankBadge",       880, 1015, 750, 862),
    ("TabActive",       880, 1015, 903, 1037),
    ("TabInactive",     880, 1015, 1063, 1198),
    ("Notification",    880, 1015, 1238, 1507),
]
HOLLOW_SET = {"PanelFrame"}
NINE_SLICE = {"PanelFrame", "HeaderFrame", "ButtonTeal", "ButtonGold", "ButtonPlay",
              "IconButton", "InputField", "Dropdown", "Notification",
              "TabActive", "TabInactive"}
V_BG, S_BG = 0.80, 0.20
MIN_AREA, PAD = 50, 3

def bg_mask(rgb):
    r, g, b = rgb[:,:,0]/255.0, rgb[:,:,1]/255.0, rgb[:,:,2]/255.0
    mx = np.maximum(np.maximum(r,g),b); mn = np.minimum(np.minimum(r,g),b)
    s = np.where(mx>0,(mx-mn)/np.maximum(mx,1e-6),0)
    return (mx>V_BG)&(s<S_BG)

def strip_label(fg):
    lbl,n = ndimage.label(fg)
    if n==0: return fg
    h = fg.shape[0]; keep=np.zeros_like(fg)
    for i in range(1,n+1):
        ys,_=np.where(lbl==i)
        top,height,area = ys.min(), ys.max()-ys.min()+1, len(ys)
        if area<MIN_AREA: continue
        if top<0.32*h and height<0.16*h: continue   # texto de etiqueta
        keep |= (lbl==i)
    return keep if keep.any() else fg

def nine(alpha):
    h,w = alpha.shape
    def gap(a):
        l=0
        while l<len(a) and a[l]: l+=1
        r=0
        while r<len(a) and a[len(a)-1-r]: r+=1
        return l,r
    l,r = gap(alpha[h//2,:]>8); t,b = gap(alpha[:,w//2]>8)
    return {"top":int(min(t,h//3)),"right":int(min(r,w//3)),
            "bottom":int(min(b,h//3)),"left":int(min(l,w//3))}

def export(rgb, bg, cell, out_dir, slices):
    name,y0,y1,x0,x1 = cell
    sub = rgb[y0:y1, x0:x1]; fg = ~bg[y0:y1, x0:x1]
    fg = strip_label(fg)
    if name in HOLLOW_SET:
        fg &= ~bg[y0:y1, x0:x1]
    else:
        fg = ndimage.binary_fill_holes(fg)
    ys,xs = np.where(fg)
    if len(ys)==0: return None
    ry0,ry1 = max(0,ys.min()-PAD), min(sub.shape[0],ys.max()+1+PAD)
    rx0,rx1 = max(0,xs.min()-PAD), min(sub.shape[1],xs.max()+1+PAD)
    crop = sub[ry0:ry1, rx0:rx1]
    alpha = fg[ry0:ry1, rx0:rx1].astype(np.uint8)*255
    img = Image.fromarray(np.dstack([crop,alpha]).astype(np.uint8),"RGBA")
    img.save(os.path.join(out_dir,name+".png"))
    if name in NINE_SLICE: slices[name]=nine(alpha)
    return img

def split_states(img,name,out_dir):
    arr=np.array(img); third=arr.shape[0]//3
    for i,st in enumerate(["normal","hover","pressed"]):
        seg=arr[i*third:(i+1)*third]
        a=seg[:,:,3]>8; ys=np.where(a.any(axis=1))[0]
        if len(ys): seg=seg[ys.min():ys.max()+1]
        Image.fromarray(seg,"RGBA").save(os.path.join(out_dir,f"{name}_{st}.png"))

def contact(paths,out_path,cols=6,cell=210):
    rows=(len(paths)+cols-1)//cols; W,H=cols*cell,rows*cell
    sheet=Image.new("RGBA",(W,H)); px=sheet.load()
    for y in range(H):
        for x in range(W):
            px[x,y]=(70,70,70,255) if ((x//12)+(y//12))%2 else (45,45,45,255)
    for i,p in enumerate(paths):
        im=Image.open(p).convert("RGBA"); im.thumbnail((cell-22,cell-22))
        cx=(i%cols)*cell+(cell-im.size[0])//2; cy=(i//cols)*cell+(cell-im.size[1])//2
        sheet.alpha_composite(im,(cx,cy))
    sheet.convert("RGB").save(out_path)

def main():
    sheet,out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir,exist_ok=True)
    rgb=np.array(Image.open(sheet).convert("RGB")); bg=bg_mask(rgb)
    slices={}; saved=[]
    for cell in CELLS:
        img=export(rgb,bg,cell,out_dir,slices)
        if img is None:
            print("  (vacio)",cell[0]); continue
        p=os.path.join(out_dir,cell[0]+".png"); saved.append(p)
        if cell[0]=="ButtonTeal": split_states(img,"ButtonTeal",out_dir)
    json.dump(slices,open(os.path.join(out_dir,"slices.json"),"w"),indent=2)
    contact(saved,os.path.join(out_dir,"_contact_sheet.png"))
    print(f"OK -> {len(saved)} assets en {out_dir} (+ slices.json + _contact_sheet.png)")

if __name__=="__main__":
    main()
