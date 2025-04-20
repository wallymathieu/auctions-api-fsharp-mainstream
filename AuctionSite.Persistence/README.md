# Persistence

In this case we use one of the simplest way possible to deal with persistence. We store events and commands to file.

## Limitations

### Concurrency

Having multiple threads calling JsonFile module could cause either file corruption or error persisting events (loosing the data).
