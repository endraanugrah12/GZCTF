# Score simulator

Open **Admin → Games → your game → Challenges → your challenge**. The scoring
section includes a **Score simulator** beside the score settings.

1. Check **Last loaded from server** to see the settings returned by the backend.
2. Choose the curve, initial points, minimum rate and decay in the edit form.
3. Set **Possible solves / teams** (for example, 10). This changes only the
   preview range; it does not automatically change the decay parameter.
4. Drag the solve-count slider to see exact base points and the marker on the graph.
5. Click **Use current count** to simulate the current challenge solve count.
6. Save the challenge to apply edited scoring settings. Refresh to verify the
   values returned by the server; repo sync may overwrite fields supplied by YAML.

The preview uses the form's values and excludes blood bonuses. No real solves,
scores or settings are changed by dragging the slider or changing its range.

With initial 500 and minimum 100, CTFd decay 9 gives 500, 496, 481, …, 100
at 1, 2, 3, …, 10 solves. Decay 50 instead gives 500, 500, 500, 499 at
the first four solves because of rounding. That sequence alone does not prove
the production configuration; compare the server values, YAML sync and deployed
version before changing anything else.
