# Don’t walk straight into the trap (Gov3)

## Abstract

## Process

### Data Extraction

#### Limitations
- structured output. Currently via "system message".

### Querying/Prompting

### Vectorization

- DB initialization, if no fitting file is present

#### Limitations
- No duplicate check on db initialization

## Future Work

In the short timeframe, not every idea and process was realistic to be realised. In this section, we want to show some further work that could be done, to improve on the existing solution prototype.

### Full Scan
Currently the check will be accomplished by extracting text data. Further improvements could be achieved by also evaluating visual data like images or video.
This could be incorporated by providing a "Full Scan" option to the user, which will take longer, but will consider more data thus being more precise.