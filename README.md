# SmartInbox.Emby

[Emby](https://emby.media/) plugin client of [this](https://github.com/alexfdezsauco/SmartInbox.Emby.Server) recommender system.

## Motivations

[How to avoid copying movies that you will never play?](http://likewastoldtome.blogspot.com/2019/12/how-to-avoid-copying-movies-that-you.html)

It turns out that I have a compulsion and obsession to watch movies. It is better to say, to copy and organize movies on my personal storage. But some of those movies will be never played.

Recently, I also noticed that I am running out of space. A well known approach to solve this situation could be to eliminate all those movies that I never played or all that I really don't like.

[Emby](https://emby.media/) also track for me all the movies that I already played and that is a perfect ground truth to be used to solve a classification problem:

 _I want to predict if I will play a movie from the following features: Official Rating, Community Rating, Critic Rating and Genres in correlation with my own playback action._
 
 ## How does it work?
 
![image](https://user-images.githubusercontent.com/1785664/208254000-aa97bbdc-b072-4e10-9dd6-d8fa75277908.png)

## References

- Original blog post [How to avoid copying movies that you will never play?](http://likewastoldtome.blogspot.com/2019/12/how-to-avoid-copying-movies-that-you.html)
- Presentation for [Python Pizza Holguín](https://holguin.python.pizza/) on [Youtube](https://www.youtube.com/watch?v=p8XdKPhTslg&t=3350s) 

![image](https://user-images.githubusercontent.com/1785664/208254382-5d4e37eb-1a79-444a-9e25-8b616633713a.png)


