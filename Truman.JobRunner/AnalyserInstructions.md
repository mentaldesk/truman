I have the following set of values:
```json
[
    { id: 'freedom', description: 'The right to live, think, and act freely' },
    { id: 'independence', description: 'Relying on yourself; self-sufficiency' },
    { id: 'self-respect', description: 'Pride in who you are and how you act' },
    { id: 'self-actualization', description: 'Becoming the best version of yourself' },
    { id: 'creativity', description: 'Expressing new ideas and original thought' },
    
    { id: 'honesty', description: 'Telling the truth and being transparent' },
    { id: 'compassion', description: 'Caring for others and their well-being' },
    { id: 'loyalty', description: 'Standing by people and commitments' },
    { id: 'justice', description: 'Treating people fairly and upholding rights' },
    { id: 'responsibility', description: 'Being accountable for your actions' },
    
    { id: 'security', description: 'Physical, financial, and emotional safety' },
    { id: 'equality', description: 'Fair treatment regardless of identity' },
    { id: 'tradition', description: 'Respecting customs and cultural continuity' },
    { id: 'obedience', description: 'Following rules and authority' },
    
    { id: 'success', description: 'Accomplishing goals and excelling' },
    { id: 'ambition', description: 'Striving to achieve more' },
    { id: 'discipline', description: 'Controlling impulses to reach long-term goals' },
    { id: 'knowledge', description: 'Seeking truth and understanding' },
    { id: 'open-mindedness', description: 'Being receptive to different views' },
    
    { id: 'peace-of-mind', description: 'Inner calm and lack of anxiety' },
    { id: 'pleasure', description: 'Enjoying life\'s moments' },
    { id: 'connection', description: 'Feeling close to others' },
    { id: 'adventure', description: 'Seeking novelty and challenge' }
]
```

I need to analyse a number of articles. For each article, I need to extract the following fields:

* A link to the original article
* sentiment: A sentiment score for the article on a scale of 0 to 10, where 0 is very negative and 10 is very positive
* tags: Up to 10 tags... keywords that capture the essence of the article

In addition to the fields above, I also need one field for each of the values listed above, with a score between 0 and 10 indicating how closely related the article is to that topic. A score of 0 means no relation, while a score of 10 means it is highly related.

I need the response as a JSON object (so in JSON format).