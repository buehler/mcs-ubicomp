# Assignment 3

## Task 1

The DSA lists the following 4 systemic risks in the first paragraph of the legal text:

- (a) spreading illegal content
- (b) any possible or expected negative impacts on fundamental rights, such as human dignity, privacy and family life, data protection, freedom of expression and access to information, equality and non-discrimination, the rights of children, and strong consumer protection
- (c) any possible or foreseeable negative impact on public votes, civic discourses, debates, and public security
- (d) any negative impact on health, public health, mental and physical well-being, protection of minors, and gender based violence

In summary: systemic risks are related to illegal actions and any actions that may negatively influence humans, well-being, mass-opinions, and fundamental rights. E.g., VLOPs may not use their recommender systems to influence the masses towards a political agenda or opinion.

Big question here is: what if the people use it to do it?

## Task 2

In general, the overall pipeline configuration in `ForYouMixerPipelineConfig` (\path{x-algorithm/home-mixer/server/src/main/scala/com/twitter/home_mixer/product/for_you/ForYouMixerPipelineConfig.scala}) defines the different subclasses that are inside the pipeline for the recommender.

### Engagement signals

Based on the overall home-mixer readme (\path{x-algorithm/home-mixer/README.md}) in the repository and analytics by codex-5, gpt5, and manual work.

#### Raw Tweet-Level Statistics

`TweetEngagementFeatureHydrator` (\path{x-algorithm/home-mixer/server/src/main/scala/com/twitter/home_mixer/product/for_you/feature_hydrator/TweetEngagementsFeatureHydrator.scala}, L18-24) defines some statistics about raw counts on a tweet level:

```scala
case class TweetEngagementCounts(
  favoriteCount: Option[Long],
  replyCount: Option[Long],
  retweetCount: Option[Long],
  quoteCount: Option[Long],
  bookmarkCount: Option[Long]
)
```

Those raw values are fetched during the pipeline execution.
The raw values are not directly used in the pipeline for the recommendation, but form the baseline for several other points.
E.g. the `ForYouExplorationTweetsCandidatePipelineConfig`, referenced in the initial mentioned pipeline config, directly uses the number of engagements. But there seems not to be a filter that orders by those absolute values.

#### Machine-Learning Scores

A lot of the algorithm relies on machine learning values. The file `ForYouScoredTweetsResponseFeatureTransformer.scala` (\path{x-algorithm/home-mixer/server/src/main/scala/com/twitter/home_mixer/product/for_you/ForYouScoredTweetsResponseFeatureTransformer.scala}, L53-137) shows a very large list (84) of features that are taken into account for its predictions (Grok is also part of it).

The following predicted scores can be highlighted:

- PredictedDwellScore
- PredictedReplyScore
- PredictedShareScore
- PredictedNegativeFeedbackV2Score

A lot of the predicted (and "phoenix", which apparently seems to be a second prediction source) values are present that influence the candidates for the home screen of an X account.

#### Real-Time Aggregations

There are a lot of aggregations (\path{x-algorithm/home-mixer/server/src/main/scala/com/twitter/home_mixer/product/scored_tweets/feature_hydrator/real_time_aggregates/TweetEngagementRealTimeAggregateFeatureHydrator.scala}, \path{x-algorithm/home-mixer/server/src/main/scala/com/twitter/home_mixer/product/scored_tweets/feature_hydrator/real_time_aggregates/UserEngagementRealTimeAggregatesFeatureHydrator.scala}, no particular line number) that influence the home screen. However, those findings are not directly used as hard facts. The realtime aggregations (user and tweet aggregations) are used as features in the ML part stated above.

The aggregations feature numbers like 30-minute counts, total counts, dwelling engagement time.

### Ranking Process

The "ForYou" process is surprisingly good documented in the readme file (\path{x-algorithm/home-mixer/README.md}). The readme states what possible candidates exist and how they are fetched and filtered. When crosschecking this with the code, it seems to match with what happens in the foryou-pipeline.

A different document ("RETRIEVAL_SIGNALS.md", \path{x-algorithm/RETREIVAL_SIGNALS.md}) describes in much detail how user behavior influences the candiate selection from the sources. Things like which authors the user recently followed and/or unfollowed are used inside ML as features to narrow down the ~1billion possible candidates to several thousand.

Overall, the ranking process does not use direct absolute values (like the engagement raw values). It rather uses the raw values as input for machine learning layers. Those features then are fed into the ML algorithm which tries to classify and select content for the home screen.

The process itself does involve the data collection, select candidates (exploration, which uses the raw values of engagement), machine learning stage (creates the scored tweets), deduplication, add "boring" stuff (like ads and "you may know this", among others).

### Relevance to systemic risks

The mentioned talk in the sources by Dr. Raphael Meier at the CYD conference in Bern (not public) also highlights that information bias is easily created. As soon as engagement is a measurement in the prediction of "likable" content, it can easily be manipulated to create engagement bubbles and foster bias. The example in the talk showed how such false flag information can be used to manipulate stock-markets and newspapers.

From this, we can clearly see the impact on engagement scores. People (malicious ones) that want to create impact and create hoaxes "just" need to generate interaction. As such, many malicious posts contain call to action parts like "like this", "give me a wish", or similar. When the algorithms - like the X one - do honor those interactions, it can be used very easily to create a systemic risk. If such a post is interacted with, the post will be recommended with a higher probability and thus gets more interaction. As soon as the post is big enough, it may also be picked up by LLMs which then "ground" their truth on those "sources". This then can even make it to media which is an indirect influence on media and deemed a systemic risk according to the DSA. Along with this example, there are many other possible applications of such malicious news creation.

## Task 3

### Where is Grok visible

One particular point is the scoring tweet mechanism in `ForYouScoredTweetsResponseFeatureTransformer.scala` (\path{x-algorithm/home-mixer/server/src/main/scala/com/twitter/home_mixer/product/for_you/ForYouScoredTweetsResponseFeatureTransformer.scala}, L101-113). Here, the Grok metrics are used as features for the machine learning algorithm. Judging by the name of some features, Grok is also used to classify and score some tweets. E.g., `GrokIsGoreFeature` or `GrokIsNsfwFeature` seem to be classifier if a tweet shows gore or nsfw (not safe for work, i.e. explicit or nude) content.

## Systemic Risk Verdict

The algorithm, together with information from the DSA and the CYD conference clearly show that as soon as interactions are incorporated into the rating and scoring process, filter-bubbles exist. Also, it is quite easy with modern tools (AI age) to create posts and engaging messages that provoke reactions and therefore get promoted to other users more often. This is effectively a systemic risk, because not only the algorithms foster such behavior, they can be used to manipulate the masses and systems like stock markets.

As an example, a verified Twitter account posted an AI fake picture where supposedly an explosion happened in the pentagon. According to the CYD talk of Dr. Meier, this fake AI picture got so much attention and engagement that it was picked up by media. This avalanche continued until the "news" was published by bigger media (not controversal media publishers) and then apparently caused the stock market to drop. As we can see with this example, such algorithms can actively be misused to manipulate critical systems like stock markets. Maybe, in this example, it was not intentional, but this can be used for malicious intents and thus also is a systemic risk on its own.

## External Sources (other than the code)

- \url{https://www.eu-digital-services-act.com/Digital_Services_Act_Article_34.html}
- \url{https://www.cydcampus.admin.ch/en/cyd-campus-conference-2025} (CYD Conference 25'), talk: Cyber Influence Operations: Threats and Countermeasures
- \url{https://edition.cnn.com/2023/05/22/tech/twitter-fake-image-pentagon-explosion}
- \url{https://www.latimes.com/business/story/2023-05-22/how-fake-ai-photo-of-a-pentagon-blast-went-viral-and-briefly-spooked-stocks}
