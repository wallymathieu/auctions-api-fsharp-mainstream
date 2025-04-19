# Persistence

In this case we use one of the simplest way possible to deal with persistence. We store events and commands to file.

We can note that here we do not try to deal with concurrency issues that could arise from getting multiple requests at the same time.