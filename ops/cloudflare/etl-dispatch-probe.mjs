const dispatchUrl =
  "https://api.github.com/repos/hubinformacion/pipeline-etl-ale/actions/workflows/etl-dispatch-probe.yml/dispatches";

export default {
  async scheduled(controller, env) {
    if (!env.GITHUB_DISPATCH_TOKEN) {
      throw new Error("Falta configurar el secreto GITHUB_DISPATCH_TOKEN.");
    }

    const scheduledAt = new Date(controller.scheduledTime).toISOString();
    const workerStartedAt = new Date().toISOString();
    const response = await fetch(dispatchUrl, {
      method: "POST",
      // Workers supports "manual"; the status check rejects redirects without forwarding the token.
      redirect: "manual",
      headers: {
        Accept: "application/vnd.github+json",
        Authorization: `Bearer ${env.GITHUB_DISPATCH_TOKEN}`,
        "Content-Type": "application/json",
        "User-Agent": "ale-etl-dispatch-probe",
        "X-GitHub-Api-Version": "2026-03-10",
      },
      body: JSON.stringify({
        ref: "main",
        inputs: {
          scheduled_at: scheduledAt,
          worker_started_at: workerStartedAt,
        },
      }),
    });

    if (!response.ok) {
      throw new Error(`GitHub rechazó el disparo (HTTP ${response.status}).`);
    }

    const { workflow_run_id: runId } = await response.json();
    console.log(JSON.stringify({
      scheduledAt,
      workerStartedAt,
      githubAcceptedAt: new Date().toISOString(),
      runId,
    }));
  },
};
