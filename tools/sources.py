"""Manifest of the original art/audio delivered by the user (Syntx / Nano Banana) and where each one lives.

The originals in the project root and in novos/ are never modified. `sync()` copies them into
Assets/Art/Source (images) and Assets/Audio/Source (audio) with descriptive names; every other tool
reads from those copies, so the pipeline is reproducible from inside the Unity project.
"""
import os
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_SRC = os.path.join(ROOT, "Assets", "Art", "Source")
AUDIO_SRC = os.path.join(ROOT, "Assets", "Audio", "Source")

# key: (original path relative to ROOT, destination relative to the Source folder, category, description)
MANIFEST = {
    # ---------------- towers (4 tiers per sheet, left to right)
    "archer": ("novos/518baa8a3ce1dabd508b786920090982_d1151db0-5d75-4805-a959-25a808e74c32.jpg", "Towers/archer_t1-t4.jpg", "torre", "Torre de Arqueiros T1-T4 (torre de vigia com corrupção)"),
    "ballista": ("novos/c4cbb6291f5350035300b5c165d5a565_d68139c0-4d07-4fa8-a672-d03417fd2413.jpg", "Towers/ballista_t1-t4.jpg", "torre", "Balista T1-T4 (painéis separados por linhas pretas)"),
    "pyre": ("novos/a1b53eccdda3f60c61a0b71ae87534ad_b4cbe987-6856-46b7-b064-9d2053abcaea.jpg", "Towers/pyre_t1-t4.jpg", "torre", "Pira Amaldiçoada T1-T4 (braseiro de fogo roxo)"),
    "chapel": ("novos/bd59d8523976b52852c49e7364699403_39434a83-9e8d-4699-8de8-b59a87c076e0.jpg", "Towers/chapel_t1-t4.jpg", "torre", "Capela de Guerra T1-T4"),
    "crow": ("novos/73c84f44f0c7052b465b1315d843e6ca_34cda68b-005b-45a0-99b3-e8c528e3e95f.jpg", "Towers/crow_t1-t4.jpg", "torre", "Torre do Corvo T1-T4"),
    "obelisk": ("novos/de43f61c831eb96741c00bc2b8204565_a8d294dc-637b-4479-84e8-70372f71f6f1.jpg", "Towers/obelisk_t1-t4.jpg", "torre", "Obelisco de Sangue T1-T4"),
    "spikes": ("novos/108c441433f93ce57a19babf4a6fd338_a78adcd4-a1e5-46c7-bd45-25b43e1e9178.jpg", "Towers/spike_trap_t1-t4.jpg", "torre", "Armadilha de Estacas T1-T4"),
    # ---------------- buildings / map pieces
    "fortress": ("182d48e550a9197e478cf405921e2ab2_2d6f1691-2679-4040-8d3a-02bbb6ad031d.jpg", "Buildings/fortress_black_tower.jpg", "construção", "Torre Negra (fortaleza), xadrez falso pintado"),
    "watchtower": ("novos/27aff2a91576730a7ec4fa9f48a39ef0_04e63dbd-f95f-439d-b5ff-419983eafe12.jpg", "Buildings/watchtower_stone.jpg", "construção", "Torre de pedra com telhado vermelho e arqueiro (anexo da fortaleza)"),
    "chapel_alt": ("novos/54ecb1a708cc2fd94684da1fee17226e_b2a470e8-a650-492f-a557-e62884d0b3a2.jpg", "Buildings/chapel_alt_levels1-4.jpg", "construção", "Variante de capela com rótulos Level 1-4 (santuário usado como cenário)"),
    "wood_tower_old": ("a6b7b87be9e19d1c1ff4f087a3f29008_e3005c07-6847-4303-ba19-8f8ece1db1ef.jpg", "Buildings/wooden_watchtower_old.jpg", "construção", "Torre de madeira antiga (protótipo), xadrez falso"),
    # ---------------- tiles + props
    "map1_tiles": ("novos/0d7d94bbde391427ada9ca409d901326_fd80d6b6-ca3d-4486-91b7-56c6e0d01ac1.jpg", "Tiles/map1_road_dirt_props.jpg", "tiles/cenário", "Mapa 1: calçamento 3x3, terra 3x3, transição, ruína, cruz, ossos, tocha, árvore morta, rocha, poço de corrupção"),
    "map2_tiles": ("novos/38e2bb65fc3058120a6cb05d77299092_c55cee27-571f-4622-9230-36d4a418a6ee.jpg", "Tiles/map2_swamp_tiles_props.jpg", "tiles/cenário", "Mapa 2: lama, água negra, tábuas, raízes, juncos, ossadas, poça de lodo, rocha, cogumelos, totens"),
    "allies_sheet": ("ba12be6a8910117fc699c32d5082ce10_84f3fa25-9753-4a3f-8b24-2a3a761b4ff5.jpg", "Props/sheet_knights_hooded_ruins_banners.jpg", "cenário/inimigos", "Folha antiga: 25 cavaleiros, encapuzados, ruínas, tochas, estandartes"),
    # ---------------- commander + skins
    "commander_sheet": ("Tríptico de Cavaleiros em Pixel Art.png", "Commander/commander_design_sheet.png", "comandante", "Comandante aprovado: frente (SE) e costas (NE) + soldado de referência"),
    "commander_sheet_alt": ("Trio de Cavaleiros em Pixel Art.png", "Commander/commander_design_sheet_alt.png", "comandante", "Variante da folha do comandante"),
    "commander_walk_a": ("532e4872f7174f9061dc837e709d638e_a38847ae-95e7-48dd-ac81-75fa1fccc8fb.jpg", "Commander/commander_walk_strip_a.jpg", "comandante", "Tira de 4 poses quase idênticas (inutilizável como ciclo de caminhada)"),
    "commander_walk_b": ("02c6e8a27aca631901e278eb5fd690a8_e3fe0c25-7635-4956-a354-7620809efb6e.jpg", "Commander/commander_walk_strip_b.jpg", "comandante", "Idem"),
    "commander_walk_c": ("3c1e7a8a3696c0412ff153c9e601c692_61397d6a-c529-40e0-bea6-9ffdef16422e.jpg", "Commander/commander_walk_strip_c.jpg", "comandante", "Idem"),
    "commander_walk_white": ("4620ab7aac23a83c6d3b5829b1e14fdd_66a4b376-f204-4fb0-95c4-f72442aa8f54.jpg", "Commander/commander_walk_strip_whitebg.jpg", "comandante", "Idem, fundo branco"),
    "commander_walk_overlap": ("4445ee48dd5d32647a2880d6f4ee950e_840135fd-6de0-4389-9d84-9f2b00bb8759.jpg", "Commander/commander_walk_strip_overlapping6.jpg", "comandante", "6 poses sobrepostas (não separáveis)"),
    "skin_black_swordsman": ("novos/d0384e68b93f020ce7f8fab1ced95240_0bac2109-96b3-4426-b46d-0bef4bbb07ed.jpg", "Commander/skin_black_swordsman_front_back.jpg", "skin", "Espadachim Negro (encapuzado, espada larga às costas) frente e costas"),
    # ---------------- enemies
    "cursed_soldier": ("7e12e66a9361812354cd3d1187c030b5_ea967247-24e5-42ab-b30f-bfccd01bad4b.jpg", "Enemies/cursed_soldier.jpg", "inimigo", "Soldado Amaldiçoado (orc morto-vivo, espada e escudo), xadrez falso"),
    "black_knight": ("9e8a1bacd2c5c2e5b6e8e68498b972d7_1d64f5fe-e2f6-4a39-b2d9-37e8eedd0da6.jpg", "Enemies/black_knight_front_profile.jpg", "inimigo", "Cavaleiro Negro frente + perfil, xadrez falso"),
    "boss_cursed_knight": ("da63c9a8d879df839b8b51867e58f786_25a316fa-0377-480a-8817-7c3f3df439d8.jpg", "Enemies/boss_cursed_knight.jpg", "boss", "Boss Cavaleiro Amaldiçoado (pixel grosso), texto embaixo, xadrez falso"),
    "hellhound_static": ("novos/0e68028c36ad6cbc1da3379d55e03492_daac49bf-00e8-495d-b1a3-1e396686e52a.jpg", "Enemies/rotting_hound_two_views.jpg", "inimigo", "Cão podre com costelas expostas, 2 vistas (usado como Carniçal)"),
    "hellhound_run": ("novos/85b8e073658d646ec67d78daa58816eb_51590991-4f2b-4f89-bf77-417e26ff6d99.jpg", "Enemies/hellhound_run_cycle_6f.jpg", "inimigo", "Cão Infernal: ciclo de corrida real, 6 quadros (2 linhas com linha de chão)"),
    "swamp_guardian": ("novos/84b92a3b4ada3083bd8d28c7e0444169_c660341d-816a-4054-919f-9db8fdee2905.jpg", "Enemies/boss_swamp_guardian_front_back.jpg", "boss", "Guardião do Pântano: cavaleiro com braço de raízes e alabarda, frente e costas"),
    # ---------------- UI
    "ui_icons": ("novos/3bee0f7540c291dc571a29f7d6180696_fe414194-cd26-463f-bad3-6507b4507bdb.jpg", "UI/ui_icons_14.jpg", "ícones", "14 ícones emoldurados em fundo cinza"),
    "ui_mockup": ("novos/dd601c42450a605b070e9016b0162978_5c74fec1-bfe6-4899-8b19-bde17574bd72.jpg", "UI/ui_mockup_4screens.jpg", "UI", "Mockup: HUD, menu (catedral), comandante (biblioteca), skins (arsenal)"),
}

AUDIO = {
    "hellhound_growl_idle": ("novos/115772_1790815777_1.mp3", "hellhound_idle_warning_growl.mp3", "Hellhound Idle Warning Growl (8.5 s)"),
    "hellhound_growl": ("novos/115772_1790815777_2.mp3", "hellhound_warning_growl.mp3", "Hellhound Warning Growl (13.2 s)"),
}


def path(key):
    return os.path.join(ART_SRC, MANIFEST[key][1])


def audio_path(key):
    return os.path.join(AUDIO_SRC, AUDIO[key][1])


def sync():
    for key, (orig, dest, _, _) in MANIFEST.items():
        d = os.path.join(ART_SRC, dest)
        if not os.path.exists(d):
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(os.path.join(ROOT, orig), d)
    for key, (orig, dest, _) in AUDIO.items():
        d = os.path.join(AUDIO_SRC, dest)
        if not os.path.exists(d):
            os.makedirs(AUDIO_SRC, exist_ok=True)
            shutil.copy2(os.path.join(ROOT, orig), d)


if __name__ == "__main__":
    sync()
    print(f"{len(MANIFEST)} images, {len(AUDIO)} audio files in Assets/*/Source")
