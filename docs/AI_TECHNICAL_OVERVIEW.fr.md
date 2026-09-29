# EchoShift Lab - Resume technique IA

Cette fiche sert a reviser vite le projet avant un entretien.

## Idee principale

Le joueur parle au robot. Le systeme transforme la voix en texte, puis transforme le texte en actions que Unity peut executer.

Phrase cle :

> L'IA ne controle pas directement le jeu. Elle propose une action structuree, puis le backend et Unity verifient avant execution.

---

## Pipeline complet

```text
voix du joueur
-> Gradium STT : audio -> texte
-> QuickIntent si commande simple
-> Gemini si commande complexe
-> backend valide les actions
-> Unity verifie la scene et execute
-> Gradium TTS fait parler ECHO
```

---

## Ou est l'IA ?

| Partie | Technologie | Role |
|---|---|---|
| Voix vers texte | Gradium STT | Transforme le son du micro en phrase texte |
| Phrase vers actions | Gemini | Comprend les commandes complexes |
| Texte vers voix | Gradium TTS | Fait parler ECHO |
| Images | Gemini Image | Genere les decors, robot, props et textures |

Important :

- `QuickIntent` n'est pas de l'IA. C'est un parser local en C# avec regex.
- Le cri, le chuchotement et le fredonnement ne sont pas de l'IA. Unity mesure directement le volume et la hauteur de voix.
- Gemini ne peut pas inventer n'importe quoi : il choisit dans une liste d'actions autorisees.

---

## Exemple 1 : commande simple

Le joueur dit :

```text
go quickly
```

Ce qui se passe :

```text
1. Gradium STT transcrit : "go quickly"
2. Unity donne la phrase a QuickIntent
3. QuickIntent reconnait "go" + "quickly"
4. Unity cree une action C#
```

```csharp
GameAction {
    Type = "MOVE_FORWARD",
    Speed = "run"
}
```

Ici, Gemini n'est pas utilise. C'est plus rapide.

---

## Exemple 2 : commande complexe

Le joueur dit :

```text
go quickly to the terminal and activate it
```

Ce qui se passe :

```text
1. Gradium STT transcrit la phrase
2. QuickIntent refuse car il y a une cible : "to the terminal"
3. Unity envoie la phrase + l'etat du monde au backend
4. Gemini analyse la phrase
5. Gemini renvoie des actions JSON
```

Exemple de reponse Gemini :

```json
{
  "actions": [
    {
      "type": "MOVE_TO",
      "target": "core_terminal",
      "speed": "run"
    },
    {
      "type": "INTERACT",
      "target": "core_terminal"
    }
  ],
  "confidence": 0.92,
  "echo": "Command accepted."
}
```

Ensuite :

```text
6. Le backend verifie que MOVE_TO et INTERACT sont autorises
7. Le backend verifie que core_terminal existe
8. Unity verifie que le robot peut vraiment aller au terminal
9. Unity execute : courir vers le terminal puis interagir
10. ECHO parle avec Gradium TTS
```

---

## A quoi sert Gemini ?

Gemini sert a faire ce mapping :

```text
phrase humaine variee -> actions connues du jeu
```

Exemples :

```text
"go to the terminal"
"walk to the blue screen"
"press the console"
"activate that glowing thing"
```

peuvent devenir :

```json
{"type": "MOVE_TO", "target": "core_terminal"}
{"type": "INTERACT", "target": "core_terminal"}
```

La liste d'actions sert de cadre. Gemini choisit dedans.

Actions possibles :

```text
MOVE_TO
MOVE_FORWARD
JUMP_OVER
CROUCH
INTERACT
BUILD
STOP
ECHO
RECYCLE
```

---

## Validation

Il y a deux validations.

### 1. Validation backend

Le backend verifie :

- l'action existe dans la liste autorisee ;
- la cible existe dans les objets visibles ;
- Gemini n'a pas invente un objet ;
- la confidence est suffisante.

Exemple :

```json
{"type": "MOVE_TO", "target": "dragon"}
```

Si `dragon` n'existe pas, l'action est rejetee.

### 2. Validation Unity

Unity verifie la realite de la scene :

- le robot peut-il avancer ?
- y a-t-il un obstacle ?
- faut-il sauter ou s'accroupir ?
- l'objet est-il utilisable ?

Exemple :

Le joueur dit :

```text
crouch under the beam
```

Mais l'obstacle est un laser au sol.

Unity comprend :

```text
CROUCH est mauvais ici, il fallait JUMP_OVER.
```

Donc le robot refuse et reagit.

---

## Si le joueur dit une action inconnue

Exemple :

```text
sleep
```

Ce qui se passe :

```text
1. STT transcrit "sleep"
2. QuickIntent ne reconnait pas
3. Gemini essaie de choisir une action autorisee
4. Il n'y a pas d'action SLEEP
5. Le systeme refuse
6. Le robot ne fait rien
```

---

## TTS : comment ECHO parle

TTS signifie **Text To Speech**.

Le TTS ne choisit pas la phrase. Il transforme une phrase deja choisie en audio.

Exemples de phrases :

```text
Command accepted.
Which object?
I can't see that from here.
Hold V and SHOUT at the glass!
```

Pipeline :

```text
texte ECHO
-> Gradium TTS
-> audio
-> Unity joue la voix
```

---

## Phrase a dire en entretien

> J'ai cree un jeu Unity controle par la voix. Gradium STT transforme la voix du joueur en texte. Pour les commandes simples, Unity utilise un parser local appele QuickIntent afin d'eviter la latence. Pour les commandes complexes, Gemini transforme la phrase en actions JSON structurees, comme MOVE_TO ou INTERACT. Le backend valide ces actions, puis Unity verifie qu'elles sont possibles dans la scene avant de les executer. J'utilise aussi Gradium TTS pour faire parler ECHO et Gemini Image pour generer les assets visuels.

Version courte :

> L'IA sert a comprendre la voix naturelle du joueur et a la convertir en actions structurees, mais le jeu garde toujours le controle final.

