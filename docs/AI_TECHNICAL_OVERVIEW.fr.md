# EchoShift Lab - Documentation technique IA

Cette note sert de rappel technique pour expliquer le projet plus tard, notamment en entretien IA / ML Engineer.

L'idee principale du projet est la suivante :

> Le joueur parle naturellement. Le systeme transforme sa voix en texte, puis transforme ce texte en actions structurees que Unity peut valider et executer.

L'IA ne controle jamais directement le jeu. Elle aide a comprendre l'intention du joueur.

---

## 1. Vue d'ensemble du pipeline

```text
Voix du joueur
  -> Unity capture le micro
  -> backend /ws/stt
  -> Gradium STT transforme audio -> texte
  -> Unity recoit la phrase finale
  -> QuickIntent si la commande est simple
  -> sinon backend /api/interpret
  -> Gemini transforme texte -> actions JSON
  -> backend valide les actions
  -> Unity verifie la faisabilite dans la scene
  -> CommandExecutor execute le plan
  -> ECHO repond
  -> Gradium TTS transforme texte -> voix
```

Le projet combine donc :

- STT : speech-to-text, pour transcrire la voix.
- LLM intent parsing : Gemini comprend les commandes complexes.
- Structured output : Gemini doit retourner du JSON valide.
- Guardrails : le backend et Unity filtrent les actions.
- TTS : text-to-speech, pour faire parler ECHO.
- Image generation : Gemini Image genere les assets visuels.

---

## 2. Ou est l'IA dans le projet ?

| Partie | Technologie | Role |
|---|---|---|
| Voix vers texte | Gradium STT | Convertit l'audio du micro en phrase texte |
| Texte vers intention | Gemini | Convertit une phrase complexe en actions structurees |
| Voix de ECHO | Gradium TTS | Convertit une phrase texte en audio |
| Images du jeu | Gemini Image | Genere les decors, robots, props et textures |

Important :

- `QuickIntent` n'est pas de l'IA. C'est un parser local en C# avec regex et liste de mots.
- Les mesures de volume, cri, chuchotement et fredonnement ne sont pas de l'IA. Unity mesure directement le signal audio.
- Unity reste l'autorite finale sur la physique et la logique du jeu.

---

## 3. STT : comment la voix devient du texte

STT signifie **Speech To Text**.

Dans EchoShift Lab, le STT sert uniquement a convertir la voix du joueur en texte.

Exemple :

```text
audio micro -> "run to the terminal and activate it"
```

### Etapes concretes

1. Le joueur maintient `V`.
2. Unity active le micro avec `VoiceInput`.
3. Unity envoie des chunks audio PCM a `SttSession`.
4. `SttSession` ouvre un WebSocket vers le backend :

```text
/ws/stt
```

5. Le backend cree une session `GradiumSTTSession`.
6. Les chunks audio sont envoyes a Gradium :

```python
client.stt_realtime(
    model_name="default",
    input_format="pcm_24000",
    json_config={"language": "en", "delay_in_frames": 10}
)
```

7. Gradium renvoie du texte partiel pendant que le joueur parle.
8. Quand le joueur relache `V`, Unity envoie `end`.
9. Le backend envoie un flush a Gradium.
10. Gradium renvoie la transcription finale.

Fichiers importants :

- `unity/EchoShift3D/Assets/EchoShift/Scripts/Voice/VoiceInput.cs`
- `unity/EchoShift3D/Assets/EchoShift/Scripts/Voice/SttSession.cs`
- `backend/routes/voice.py`
- `backend/audio/gradium_stt.py`

Le STT ne decide aucune action. Il produit seulement du texte.

---

## 4. QuickIntent : commandes simples sans IA

`QuickIntent` est un parser local dans Unity.

Fichier :

```text
unity/EchoShift3D/Assets/EchoShift/Scripts/Gameplay/QuickIntent.cs
```

Il sert a eviter un appel a Gemini pour les commandes simples. Cela reduit la latence.

### Ce que QuickIntent sait gerer

Exemples :

```text
jump
walk forward
go quickly
crouch
turn around
use
```

### Comment ca marche techniquement

La methode principale est :

```csharp
public static bool TryParse(string text, out List<GameAction> actions)
```

Elle fait :

1. Nettoyage du texte :

```csharp
var clean = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9' ,;]", " ").Trim();
```

2. Rejet des commandes avec destination complexe :

```text
go to the terminal
walk to the door
```

Ces commandes partent vers Gemini, car il faut identifier un objet de la scene.

3. Verification que tous les mots sont connus dans une liste simple (`HashSet<string> Filler`).

4. Mapping regex vers `GameAction`.

Exemple :

```csharp
if (Has(c, "jump|hop|leap"))
    return GameAction.Of("JUMP_OVER");
```

Donc :

```text
"jump over the laser"
```

devient en memoire C# :

```csharp
new GameAction
{
    Type = "JUMP_OVER",
    Target = ""
}
```

Ce n'est pas du JSON. C'est un objet C# cree directement dans Unity.

### Exemple : "go quickly"

La phrase :

```text
go quickly
```

est reconnue par QuickIntent :

- `go` signifie mouvement vers l'avant.
- `quickly` signifie vitesse rapide.

Resultat :

```csharp
GameAction {
    Type = "MOVE_FORWARD",
    Speed = "run"
}
```

### Exemple : "go quickly to the terminal"

Cette phrase contient une destination :

```text
to the terminal
```

QuickIntent refuse et retourne `false`.

La phrase part alors vers Gemini, qui peut produire :

```json
{
  "actions": [
    {
      "type": "MOVE_TO",
      "target": "core_terminal",
      "speed": "run"
    }
  ]
}
```

---

## 5. Gemini : comprendre les commandes complexes

Gemini sert quand une commande demande de comprendre :

- une cible precise ;
- une sequence de plusieurs actions ;
- une paraphrase ;
- une intention implicite ;
- une construction avec materiau.

Exemples :

```text
go to the terminal and activate it
walk to the blue screen and press it
build a bridge of ice over the gap
make something icy so I can cross
```

### Ce que Unity envoie au backend

Unity envoie :

- la phrase du joueur ;
- l'etat du monde ;
- les objets visibles ;
- les hazards actifs ;
- les objets interactifs.

Exemple simplifie :

```json
{
  "transcript": "go to the terminal and activate it",
  "world_state": {
    "roomName": "Core",
    "visibleObjects": [
      {
        "id": "core_terminal",
        "type": "terminal",
        "label": "terminal"
      }
    ],
    "allowedActions": ["MOVE_TO", "INTERACT", "JUMP_OVER", "CROUCH", "STOP"]
  }
}
```

Fichiers Unity :

- `GameDirector.cs` construit `WorldStateJson()`.
- `GameAction.cs` contient `IntentClient.Interpret(...)`.

### Ce que le backend envoie a Gemini

Dans `backend/ai/intent_parser.py`, le backend construit un prompt avec :

```python
prompt = {
    "player_transcript": transcript,
    "world_state": world_state,
    "allowed_actions": world_state.get("allowedActions", []),
    "response_rules": {
        "confidence_threshold": 0.7,
        "max_actions": 8,
        "no_autopilot": "Do not solve the whole room from a vague request.",
    },
}
```

Puis il appelle Gemini avec :

```python
response = await self.client.aio.models.generate_content(
    model=self.settings.gemini_model,
    contents=json.dumps(prompt),
    config=types.GenerateContentConfig(
        system_instruction=INTENT_INTERPRETER_SYSTEM_INSTRUCTION,
        response_mime_type="application/json",
        response_schema=IntentResponse,
        temperature=0.15,
    ),
)
```

Points importants :

- `response_mime_type="application/json"` force une reponse JSON.
- `response_schema=IntentResponse` force le schema attendu.
- `temperature=0.15` reduit la creativite pour avoir des reponses plus stables.

### Exemple complet

Phrase :

```text
go to the terminal and activate it
```

Gemini peut repondre :

```json
{
  "actions": [
    {
      "type": "MOVE_TO",
      "target": "core_terminal",
      "speed": "walk"
    },
    {
      "type": "INTERACT",
      "target": "core_terminal"
    }
  ],
  "confidence": 0.92,
  "interpretation": "The player wants to go to the terminal and use it.",
  "requires_clarification": false,
  "echo": "Command accepted."
}
```

Gemini ne bouge pas le robot. Il traduit une phrase naturelle en actions structurees.

---

## 6. Liste des actions predefinies

Gemini doit choisir dans une liste d'actions que le jeu sait executer.

Fichiers :

- `backend/ai/schemas.py`
- `backend/game/action_schema.py`
- `unity/EchoShift3D/Assets/EchoShift/Scripts/Gameplay/GameAction.cs`

Actions principales :

```text
MOVE_LEFT
MOVE_RIGHT
MOVE_FORWARD
MOVE_BACK
TURN_LEFT
TURN_RIGHT
TURN_AROUND
MOVE_TO
JUMP
JUMP_OVER
CROUCH
STAND
WAIT
STOP
INTERACT
USE
FOLLOW
AVOID
BUILD
ECHO
RECYCLE
```

Pourquoi une liste predefinie ?

Parce que le modele ne doit pas inventer des actions impossibles comme :

```text
FLY
TELEPORT
SLEEP
BECOME_INVISIBLE
```

La liste est le contrat entre l'IA et le jeu.

Gemini sert a choisir les bonnes actions dans cette liste, pas a inventer de nouvelles capacites.

---

## 7. Validation backend

La validation backend filtre la sortie de Gemini avant de l'envoyer a Unity.

Fichier :

```text
backend/game/validators.py
```

La fonction principale est :

```python
validate_intent(intent, world_state)
```

Elle verifie :

- l'action est dans `ALLOWED_ACTIONS` ;
- les actions avec cible pointent vers un objet visible ;
- les cibles inventees sont rejetees ;
- si toutes les actions sont invalides, le backend demande une clarification.

Exemple :

Gemini repond :

```json
{
  "actions": [
    {
      "type": "MOVE_TO",
      "target": "dragon"
    }
  ]
}
```

Mais `dragon` n'existe pas dans `visibleObjects`.

Le backend transforme la reponse en :

```json
{
  "actions": [],
  "confidence": 0.25,
  "requires_clarification": true,
  "clarification": "Which object?",
  "echo": "Which object?"
}
```

Cette validation evite qu'une hallucination de Gemini atteigne Unity.

---

## 8. Validation Unity

Apres la validation backend, Unity refait une validation physique et gameplay.

Fichier principal :

```text
unity/EchoShift3D/Assets/EchoShift/Scripts/Gameplay/CommandExecutor.cs
```

La methode importante est :

```csharp
Plan Create(GameAction a)
```

Elle recoit une action et decide quel plan d'execution creer.

### Exemple : MOVE_TO

```csharp
case "MOVE_TO":
case "FOLLOW":
{
    var e = WorldEntity.Find(a.Target);
    if (!e) return Fail("I can't see that from here.");
    return new MovePlan(this, e.Position, speed, e.ApproachDistance);
}
```

Unity verifie :

- la cible existe dans la scene ;
- l'objet a une position ;
- le robot peut marcher vers lui.

### Exemple : JUMP_OVER

```csharp
case "JUMP":
case "JUMP_OVER":
{
    var barrier = FindBarrier(a.Target) ?? NearestBarrierAhead(3.5f);
    if (barrier == null)
    {
        subject.Jump();
        return new WaitPlan(0.8f);
    }
    if (barrier.Kind == BarrierKind.Crouch)
        return director.WrongMove(barrier, false)
            ? new InstantPlan(PlanResult.Failed)
            : Fail("Too high to jump. Say \"crouch under the beam\".");
    if (barrier.Kind != BarrierKind.Jump) return Fail(barrier.Hint);
    return new JumpOverPlan(this, barrier);
}
```

Unity verifie :

- y a-t-il une barriere devant le joueur ?
- cette barriere est-elle sautable ?
- si c'est une barriere haute, sauter est une mauvaise action ;
- si c'est une barriere basse, sauter est autorise.

### Exemple : CROUCH

```csharp
case "CROUCH":
{
    var barrier = FindBarrier(a.Target) ?? NearestBarrierAhead(3.5f);
    if (barrier != null && barrier.Kind == BarrierKind.Crouch)
        return new CrouchUnderPlan(this, barrier);
    if (barrier != null && barrier.Kind == BarrierKind.Jump && director.WrongMove(barrier, true))
        return new InstantPlan(PlanResult.Failed);
    return new CrouchPlan(subject, a.DurationMs > 0 ? a.DurationMs / 1000f : 1.4f);
}
```

Unity verifie :

- si l'obstacle est haut, `CROUCH` est correct ;
- si l'obstacle est au sol, `CROUCH` est incorrect : il fallait sauter.

### Exemple concret : mauvaise action

Le joueur dit :

```text
crouch under the beam
```

Mais il est devant un laser au sol.

Pipeline :

```text
STT -> "crouch under the beam"
QuickIntent -> GameAction { Type = "CROUCH" }
CommandExecutor -> cherche la barriere devant
barrier.Kind == Jump
WrongMove(..., triedCrouch: true)
action echoue
robot reagit avec animation confuse
```

La validation Unity est donc la derniere protection : elle verifie la realite de la scene.

---

## 9. Que se passe-t-il avec une action inconnue ?

Exemple :

```text
sleep
```

Pipeline :

1. STT transcrit :

```text
sleep
```

2. QuickIntent essaie de parser.

`sleep` n'est pas dans sa liste de mots simples, donc QuickIntent retourne `false`.

3. La phrase part vers Gemini.

Gemini doit choisir dans les actions autorisees. Il n'y a pas `SLEEP`.

Il devrait repondre :

```json
{
  "actions": [],
  "confidence": 0.2,
  "requires_clarification": true,
  "clarification": "I can't do that."
}
```

4. Unity refuse d'executer :

```csharp
if (intent.Actions.Count == 0 || intent.NeedsClarification || intent.Confidence < 0.7f)
```

Resultat :

```text
Le robot ne fait rien.
ECHO demande une clarification ou dit qu'il n'a pas compris.
```

---

## 10. TTS : comment ECHO parle

TTS signifie **Text To Speech**.

Le TTS transforme une phrase texte en voix audio.

Dans le projet, le TTS utilise **Gradium TTS**.

Fichiers :

- `backend/audio/gradium_tts.py`
- `backend/routes/voice.py`
- `unity/EchoShift3D/Assets/EchoShift/Scripts/Voice/EchoSpeaker.cs`

Backend :

```python
client.tts_realtime(
    voice_id=settings.gradium_voice_id,
    output_format="wav",
    model_name="default",
)
```

Important :

> Le TTS ne choisit pas la phrase. Il transforme une phrase deja choisie en audio.

La phrase peut venir :

- du code Unity ;
- du backend ;
- du champ `echo` renvoye par Gemini ;
- d'une clarification ;
- d'un message d'erreur gameplay.

Exemples :

```text
Command accepted.
Which object?
I can't see that from here.
Hold V and SHOUT at the glass!
Too high to jump. Say "crouch under the beam".
```

Pipeline :

```text
texte ECHO
  -> backend /ws/tts
  -> Gradium TTS
  -> audio wav
  -> Unity joue l'audio
```

---

## 11. Generation d'images avec Gemini Image

Gemini Image sert a generer les assets visuels :

- fonds des salles ;
- poses du robot ;
- lasers ;
- terminal ;
- porte ;
- portail ;
- textures ;
- images du README.

Fichier :

```text
backend/ai/design_assets.py
```

Exemple de logique :

```text
prompt texte -> Gemini Image -> image PNG -> cache backend/assets -> copie Unity Resources
```

Les images sont mises en cache pour eviter de regenarer a chaque lancement.

Cette partie montre une utilisation multimodale :

```text
texte -> image
```

---

## 12. Resume pour entretien IA / ML Engineer

Phrase courte :

> J'ai construit un pipeline d'interaction vocale pour un jeu Unity. Le joueur parle, Gradium STT transcrit la voix, Gemini interprete les commandes complexes en actions JSON structurees, le backend valide ces actions, puis Unity verifie leur faisabilite physique avant execution. J'utilise aussi Gradium TTS pour la voix de l'assistant et Gemini Image pour generer les assets visuels.

Phrase plus technique :

> Le systeme separe la comprehension du langage et l'autorite gameplay. Le LLM n'execute rien directement : il produit une intention structuree dans un schema ferme. Le backend filtre les hallucinations et Unity garde la decision finale sur les collisions, obstacles et interactions. Les commandes simples passent par QuickIntent, un parser local rule-based, pour reduire la latence.

Points a valoriser :

- integration STT temps reel ;
- orchestration backend de services IA ;
- LLM intent parsing ;
- structured output avec schema Pydantic ;
- validation et guardrails ;
- fallback rule-based ;
- reduction de latence avec parsing local ;
- separation client / backend pour proteger les cles API ;
- generation d'images avec prompts et cache ;
- Unity garde l'autorite finale.

---

## 13. Resume ultra simple

```text
STT = voix -> texte
Gemini = texte -> actions structurees
Backend = verifie que l'action est logique
Unity = verifie que l'action est possible dans la scene
TTS = texte -> voix
Gemini Image = texte -> images du jeu
QuickIntent = parser local, pas IA
```

