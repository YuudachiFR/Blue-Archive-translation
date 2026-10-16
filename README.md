# BA Traduction

Joue à **Blue Archive** sur PC **en français**, sur le client **JP** (Yostar) comme sur la version **Global** (Steam).

Les textes du jeu sont traduits automatiquement par une IA qui tourne **sur ton PC** : pas de compte, pas de clé, rien n'est envoyé sur internet. Une phrase n'est traduite qu'une seule fois ; ensuite elle s'affiche instantanément en français.

---

## Installation pas à pas

> Compte environ 15 minutes, plus le temps de téléchargement du modèle d'IA (≈ 9 Go).

### Étape 1 — Installer Ollama (le moteur d'IA)

1. Va sur **https://ollama.com/download** et clique sur **Download for Windows**.
2. Ouvre le fichier téléchargé (`OllamaSetup.exe`) et clique sur **Install**.
3. C'est tout : Ollama se lance tout seul en arrière-plan (petite icône de lama près de l'horloge).

### Étape 2 — Télécharger BA Traduction

1. En haut de cette page GitHub, dans la liste des fichiers, clique sur **`BATraduction.exe`**.
2. Sur la page qui s'ouvre, clique sur le bouton **Download raw file** (flèche vers le bas, à droite).
3. Range le fichier dans un dossier à toi, par exemple `Documents\BA Traduction`. Ses données (cache, réglages, corrections) seront créées à côté, dans un sous-dossier `Données` : tout reste au même endroit, et il suffit de copier ce dossier pour sauvegarder ou déplacer BA Traduction. Évite `Program Files`, où l'appli ne peut pas écrire.

> Rien d'autre à installer : tout est inclus dans le fichier.

### Étape 3 — Premier lancement

1. Double-clique sur **`BATraduction.exe`**.
2. Si Windows affiche **« Windows a protégé votre ordinateur »** : clique sur **Informations complémentaires**, puis **Exécuter quand même**. (Ce message apparaît pour tous les programmes non signés.)
3. En haut de la fenêtre, clique sur **Télécharger le modèle**. Attends que le message devienne vert : **« Traducteur actif »**. Le téléchargement peut prendre de quelques minutes à une heure selon ta connexion.

### Étape 4 — Activer la traduction dans le jeu

1. **Ferme le jeu** s'il est ouvert.
2. Dans BA Traduction, sur la carte de ta version (**JP** ou **Global**), clique sur **Installer**.
   Elle doit afficher **● Traduction ACTIVÉE** en vert.
   Si le jeu n'est pas trouvé, clique sur **Changer de dossier…** et choisis le dossier qui contient `BlueArchive.exe`.

### Étape 5 — Jouer

1. Lance BA Traduction (si ce n'est pas déjà fait).
2. Lance le jeu **normalement** (launcher Yostar pour le JP, Steam pour la Global).
3. **Laisse BA Traduction ouverte** pendant que tu joues. Tu peux la réduire : elle se range près de l'horloge.

Un texte jamais vu reste en version originale une ou deux secondes, puis passe en français. La toute première traduction de la session peut prendre jusqu'à 40 secondes (chargement de l'IA).

---

## Choisir une autre langue

Le français est la langue par défaut. Pour traduire le jeu dans une autre langue : **Langue et moteur…** → **Traduire vers**, puis **Enregistrer** et relance le jeu.

Langues proposées : français, English, español, Deutsch, italiano, português (Brasil), polski, русский, Türkçe, Bahasa Indonesia, tiếng Việt, 한국어, 简体中文, 繁體中文.

> Les langues à alphabet non latin ou à nombreux accents (russe, coréen, chinois, vietnamien) peuvent mal s'afficher si la police du jeu ne contient pas leurs caractères.

Chaque langue a son propre cache : changer de langue puis revenir ne fait rien perdre.

---

## Utiliser une IA en ligne (optionnel)

Par défaut, la traduction se fait **en local et gratuitement** avec Ollama. Tu peux à la place utiliser l'IA de ton choix avec une **clé API** : la traduction est en général meilleure, et il n'y a pas besoin d'Ollama ni d'une grosse carte graphique, mais l'utilisation est **payante** chez la plupart des fournisseurs.

1. Dans BA Traduction, clique sur **Langue et moteur…**
2. Choisis le moteur :
   - **Claude (Anthropic)** : crée une clé sur **https://console.anthropic.com** (rubrique *API Keys*) et colle-la dans **Clé API**. Choisis le modèle dans la liste.
   - **Autre IA** : choisis le **fournisseur** dans la liste, colle ta **clé** et indique le **nom exact du modèle** tel qu'il apparaît chez le fournisseur. L'adresse est préremplie.

     | Fournisseur | Où créer la clé |
     |---|---|
     | OpenAI (ChatGPT) | platform.openai.com |
     | Google Gemini | aistudio.google.com |
     | Mistral | console.mistral.ai |
     | DeepSeek | platform.deepseek.com |
     | xAI (Grok) | console.x.ai |
     | Groq | console.groq.com |
     | OpenRouter (des centaines de modèles de tous les fournisseurs) | openrouter.ai |
     | Together AI | api.together.ai |
     | Qwen (Alibaba Cloud) | alibabacloud.com |
     | Moonshot (Kimi) | platform.moonshot.ai |
     | LM Studio (IA locale) | pas de clé |
     | **Autre (adresse personnalisée)** | n'importe quel service compatible avec le format OpenAI : indique son adresse d'API |

   - **DeepL** : traducteur spécialisé, avec une offre gratuite. Crée une clé « DeepL API » sur **deepl.com**. Le glossaire ne s'applique pas à DeepL, mais tes corrections oui.
3. Clique sur **Tester** : une phrase d'essai doit s'afficher dans la langue choisie.
4. Clique sur **Enregistrer**. Coche **« Retraduire les phrases déjà en cache pour cette langue »** si tu veux que tout soit retraduit avec le nouveau moteur.
5. Relance le jeu.

Chaque fournisseur garde sa propre clé et son propre modèle : tu peux passer de l'un à l'autre sans tout ressaisir. Les clés sont enregistrées **chiffrées** sur ton PC (liées à ta session Windows) et ne sont envoyées qu'au fournisseur choisi. Pour revenir au mode gratuit : **Langue et moteur… → Local (Ollama)**.

---

## Au quotidien

| Je veux… | Je fais… |
|---|---|
| Jouer en français | Ouvrir BA Traduction, puis lancer le jeu |
| Jouer en version originale | Jeu fermé : bouton **Désactiver** |
| Remettre le français | Jeu fermé : bouton **Activer** |
| Tout enlever | Jeu fermé : bouton **Désinstaller** |
| Corriger une traduction | Dans le **Journal**, clic droit sur la ligne → **Corriger…**, **Retraduire** ou **Oublier** (appliqué en jeu en quelques secondes) |

---

## Carte graphique

La carte **Carte graphique** de l'appli affiche en temps réel (cartes NVIDIA) :

- l'**utilisation** du processeur graphique, avec un graphique de la dernière minute ;
- la **mémoire vidéo (VRAM)** utilisée et totale ;
- la **température** ;
- le **modèle d'IA chargé** et la place qu'il occupe en VRAM ;
- un **diagnostic pour l'IA locale**, avec une pastille de couleur :

| Pastille | Signification |
|---|---|
| 🟢 Idéal pour l'IA locale | La carte a largement assez de mémoire vidéo : traduction locale rapide et gratuite |
| 🔵 Adapté / IA en ligne active | Ça fonctionne, ou une IA en ligne est utilisée (l'appli indique si le local serait possible) |
| 🟠 Juste / mémoire occupée / trop chaude | Risque de lenteurs : ferme d'autres applications, prends un modèle plus léger ou passe sur une IA en ligne |
| 🔴 Puissance insuffisante | La carte n'a pas assez de mémoire vidéo : utilise une IA en ligne (DeepL gratuit, Gemini, Claude…) |

Le modèle local occupe environ **9 Go de VRAM** tant qu'il est chargé. Pendant une partie, il reste chargé en permanence pour éviter toute attente ; à la fermeture du jeu, la VRAM est libérée automatiquement. En dehors du jeu, il se libère tout seul après quelques minutes sans traduction. Pour récupérer cette mémoire tout de suite (par exemple avant un autre jeu), clique sur **Libérer la VRAM**, aussi disponible en clic droit sur l'icône près de l'horloge. Le modèle se recharge automatiquement à la prochaine traduction.

Si Ollama est fermé, BA Traduction le relance tout seul au démarrage, ou via le bouton **Démarrer Ollama**.

---

## En cas de problème

| Problème | Solution |
|---|---|
| Point rouge « Ollama… » | Ollama est fermé ou absent : bouton **Démarrer Ollama** (ou **Installer Ollama** s'il n'est pas encore installé). |
| Les textes restent en japonais / anglais | Vérifie que BA Traduction est **ouverte** et affiche **Traducteur actif** en vert, et que la carte du jeu indique **ACTIVÉE**. |
| Le jeu ne se lance plus | Jeu fermé : clique sur **Désactiver**. Le jeu redevient d'origine. |
| « Une autre XInput1_3.dll est présente » | Un autre outil de traduction est installé dans le jeu. **Installer** la met de côté et la remplace. N'utilise pas les deux en même temps. |
| « Le port 5050 est déjà utilisé » | BA Traduction est déjà ouverte (regarde près de l'horloge). |
| Traduction très lente | L'IA a besoin d'une carte graphique récente (12 Go de mémoire vidéo conseillés). |

---

## Fiabilité et qualité

- **Priorité à ce que tu lis** : le texte le plus récent est traduit en premier, et les textes qui ne sont plus à l'écran sont ignorés. Un menu rempli de libellés ne bloque donc plus les dialogues.
- **Rien n'est perdu** : si une traduction échoue (IA occupée, Ollama qui démarre…), elle est réessayée automatiquement jusqu'à 4 fois.
- **Pas de morceaux de phrase** : un texte qui s'écrit lettre par lettre n'est traduit qu'une fois complet. Si des morceaux apparaissent encore, augmente la **Pause** dans **Langue et moteur…** (par ex. 1 200 ms).
- **Contexte** : les dernières répliques sont transmises à l'IA pour garder le ton et le sens d'un dialogue.
- **Contrôle qualité** : une traduction vide, trop longue, trop courte, encore en japonais ou accompagnée d'une note est refaite automatiquement ; si elle reste mauvaise, elle n'est pas enregistrée.
- **Correction en deux clics** : clic droit sur une ligne du **Journal** :
  - **Retraduire** : refait la traduction tout de suite ;
  - **Corriger…** : tu écris toi-même la bonne traduction (enregistrée dans tes corrections) ;
  - **Oublier** : la traduction sera refaite au prochain affichage.

  Le jeu reçoit la correction en quelques secondes, sans redémarrer.

---

## Personnaliser les traductions

Bouton **Dossier des données** dans l'appli (le sous-dossier `Données` à côté de `BATraduction.exe`) :

| Fichier | À quoi il sert |
|---|---|
| `corrections.tsv` | Tes corrections pour le français, prioritaires sur tout. Une ligne par correction : `texte original` + **Tab** + `ta traduction`. Pour une autre langue : `corrections.<code>.tsv` (ex. `corrections.es.tsv`). |
| `glossaire.md` | Les termes imposés à l'IA (Sensei, élèves, Kivotos, noms des menus…). |
| `cache.jsonl` | Toutes les traductions déjà faites. Le supprimer fait tout retraduire. |

Après une modification : ferme et relance BA Traduction, puis le jeu.

---

## Configuration requise

- Windows 10 ou 11, 64 bits
- Mode local : carte graphique avec 12 Go de mémoire vidéo ou plus (recommandé) et ≈ 10 Go d'espace disque
- Mode clé API (Claude ou autre) : une connexion internet suffit

---

## Pour les développeurs

### Fonctionnement

```
Jeu ──► XInput1_3.dll ──► BA Traduction (127.0.0.1:5050) ──► Ollama (qwen2.5:14b)
                                                          ├─► API Claude (SDK Anthropic)
                                                          ├─► API compatible OpenAI (Gemini, Mistral, DeepSeek…)
                                                          └─► API DeepL
```

Réglages du moteur : `moteur.json` dans le dossier des données (clés chiffrées avec DPAPI).

- `XInput1_3.dll` est chargée par le jeu, intercepte `UILabel.set_text`, remplace les textes connus et envoie les autres au traducteur ; les appels manette sont transmis à la XInput de Windows.
- `BATraduction.exe` gère l'installation par version du jeu et sert les traductions (cache, glossaire, corrections).

### Réglages avancés

`BAtrad.ini` dans le dossier du jeu :

| Clé | Défaut | |
|---|---|---|
| `from` | `ja` (JP) / `en` (Global) | Langue source |
| `port` | `5050` | Port du traducteur |
| `debounce_ms` | `800` | Délai avant de traduire un texte qui change (réglable dans **Langue et moteur… → Pause**) |
| `assembly` / `class` / `setter` | `BlueArchive.dll` / `UILabel` / `set_text` | Fonction d'affichage interceptée |

Journal de la DLL : `BAtrad.log` dans le dossier du jeu.

### Compilation

Prérequis : Visual Studio 2022 (charge de travail C++) et le SDK .NET 8 ou plus récent.

```bat
dll\build.bat
dotnet publish app\BATraduction.csproj -c Release -o app\publish
```

Résultat : `app\publish\BATraduction.exe`, autonome (runtime .NET et DLL intégrés).

### Structure

```
BATraduction.exe  Application prête à l'emploi
app/          Application BA Traduction (C# / WinForms)
dll/          XInput1_3.dll (C++) et MinHook (licence BSD, voir dll/minhook/LICENSE.txt)
glossaire.md  Glossaire par défaut
```

---

## Avertissement

Projet personnel non officiel, sans lien avec Nexon, Yostar ou NEXON Games. Modifier le client du jeu peut être contraire à ses conditions d'utilisation et comporte un risque de sanction sur le compte. À utiliser à tes propres risques.
