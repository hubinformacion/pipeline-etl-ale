const dispatchUrl =
  "https://api.github.com/repos/hubinformacion/pipeline-etl-ale/actions/workflows/etl-neon.yml/dispatches";

const nightlyCron = "7 3 * * *";
const limaClock = new Intl.DateTimeFormat("en", {
  timeZone: "America/Lima",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  hourCycle: "h23",
});

function limaDateAndHour(instant) {
  const parts = Object.fromEntries(
    limaClock.formatToParts(instant).map(({ type, value }) => [type, value]),
  );
  return { date: `${parts.year}-${parts.month}-${parts.day}`, hour: Number(parts.hour) };
}

export default {
  async scheduled(controller, env) {
    if (controller.cron !== nightlyCron) {
      console.log("Cron ignorado. El disparo nocturno usa 7 3 * * * en UTC.");
      return;
    }

    const scheduledTime = new Date(controller.scheduledTime);
    const workerStart = new Date(Date.now());
    const referenceDate = limaDateAndHour(scheduledTime).date;
    const local = limaDateAndHour(workerStart);
    if (local.date !== referenceDate || local.hour < 22 || workerStart < scheduledTime) {
      throw new Error(`Disparo fuera de la ventana nocturna de ${referenceDate} en Lima.`);
    }

    if (!env.GITHUB_DISPATCH_TOKEN) {
      throw new Error("Falta configurar el secreto GITHUB_DISPATCH_TOKEN.");
    }

    const scheduledAt = scheduledTime.toISOString();
    const workerStartedAt = workerStart.toISOString();
    const response = await fetch(dispatchUrl, {
      method: "POST",
      // Workers supports "manual"; the status check rejects redirects without forwarding the token.
      redirect: "manual",
      headers: {
        Accept: "application/vnd.github+json",
        Authorization: `Bearer ${env.GITHUB_DISPATCH_TOKEN}`,
        "Content-Type": "application/json",
        "User-Agent": "ale-etl-dispatch",
        "X-GitHub-Api-Version": "2026-03-10",
      },
      body: JSON.stringify({
        ref: "main",
        inputs: {
          reference_date: referenceDate,
          source: "all",
        },
      }),
    });

    if (!response.ok) {
      throw new Error(`GitHub rechazó el disparo (HTTP ${response.status}).`);
    }

    const { workflow_run_id: runId } = await response.json();
    console.log(JSON.stringify({
      referenceDate,
      scheduledAt,
      workerStartedAt,
      githubAcceptedAt: new Date().toISOString(),
      runId,
    }));
  },
};
