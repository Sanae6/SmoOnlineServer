# Deploy to Fly.io

## What is Fly.io?

[Fly.io](https://www.fly.io) is a platform for running applications in the cloud. Their tooling builds
an image from a Dockerfile and runs it in a datacenter without you having to manage the server yourself.
They have a free tier that provides more than enough power to run the SMO Online server code.

Note: Fly *does* require a credit card on signup, but this is only to prevent bots from abusing their free tier. The setup listed in this guide is completely covered by their free tier, so you should not incur any charges.

## Deployment Instructions

1. First, follow Fly's [Installing flyctl](https://fly.io/docs/getting-started/installing-flyctl/) and
[Log in](https://fly.io/docs/getting-started/log-in-to-fly/) instructions to get your computer set up
to interact with Fly.
    * `flyctl` is Fly's tool for creating and deploying applications.
2. Clone this repo using `git clone https://github.com/Sanae6/SmoOnlineServer.git` in your terminal.
3. Run `cd SmoOnlineServer` to enter the repo directory.
4. Run `flyctl launch`.
    * This is the command for creating a new application on Fly. It will ask for a name to use (must be globally unique) and a region
    to deploy to, you should pick a region close to you if it did not pick one automatically.
    * This command generates a `fly.toml` file in the repo, which is the configuration that Fly looks at when
    deciding how to run your application.
5. By default this `fly.toml` file assumes that you are deploying a standard HTTP application, but this server operates over raw TCP. You will need to edit this file to look like the following:
    *   ```toml
        app = "<your-app-name>"
        kill_signal = "SIGINT"
        kill_timeout = 5
        processes = []

        [env]

        [experimental]
          allowed_public_ports = []
          auto_rollback = true

        [[services]]
          http_checks = []
          internal_port = 1027
          processes = ["app"]
          protocol = "tcp"
          script_checks = []
  
          [services.concurrency]
            hard_limit = 25
            soft_limit = 20
            type = "connections"

          [[services.ports]]
            port = 1027
        ```
    * This configuration tells Fly that you are running the app over plain TCP, not HTTP, and to expose
    port 1027 instead of ports 80 and 443 like the default configuration will.
6. Run `flyctl deploy`.
    * This will build the server code into a Docker image and push it to Fly, where it will then run in
    their datacenter in the region that you chose. This step might take a few minutes to complete.
7. Now that your app is running, you can log in to Fly in your browser. Under your [Apps](https://fly.io/dashboard/personal)
page you should see the application, and clicking on it will bring you to the Overview page.
    * The most important thing here is the "IP addresses" box near the middle of the page. When you start the
    modded game, this IP will be the one you want to enter (use the one with "v4" next to it).
    * Another useful page is the Monitoring page on the left, which will show you useful logs about when users
    connect, when moons are picked up, etc.

And that's it! If you've made it this far, you should have the server up and running for free. By default Fly allocates
one CPU core and 256MB of RAM for their free tier, which should be plenty.